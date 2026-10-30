using System.Security.Cryptography;
using System.Xml;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pervaxis.Vpa.Vending.Data;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

public sealed class BillingImportService(VendingDbContext dbContext, PosWorkbookReader workbookReader)
{
    public const long MaximumUploadBytes = 15 * 1024 * 1024;

    public async Task<BillingImportResponse> UploadAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw InvalidFile("Select a non-empty .xlsx workbook to upload.");
        if (file.Length > MaximumUploadBytes)
            throw InvalidFile("The workbook exceeds the 15 MB upload limit.");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            throw InvalidFile("Only .xlsx workbooks are supported.");

        await using var memory = new MemoryStream((int)file.Length);
        await file.CopyToAsync(memory, cancellationToken);
        var bytes = memory.ToArray();
        if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B)
            throw InvalidFile("The uploaded file is not a valid .xlsx workbook.");
        var contentSha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var existingImport = await dbContext.BillingImports
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.ContentSha256 == contentSha256, cancellationToken);
        if (existingImport is not null)
            throw Duplicate(existingImport);

        memory.Position = 0;
        PosWorkbookResult parsed;
        try
        {
            parsed = workbookReader.Read(memory);
        }
        catch (InvalidPosWorkbookException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw InvalidFile("The uploaded file is not a valid .xlsx workbook.");
        }
        catch (XmlException)
        {
            throw InvalidFile("The uploaded file contains an invalid workbook document.");
        }

        if (parsed.Issues.Count > 0)
            throw new InvalidPosWorkbookException(parsed.Issues);

        var now = DateTimeOffset.UtcNow;
        var safeFileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (safeFileName.Length > 255)
            throw InvalidFile("The workbook filename must be 255 characters or fewer.");
        var import = new BillingImport
        {
            Id = Guid.NewGuid(),
            OriginalFileName = safeFileName,
            ContentSha256 = contentSha256,
            Status = "Validated",
            TransactionCount = parsed.Transactions.Count,
            RowsWithoutProductName = parsed.RowsWithoutProductName,
            CreatedAt = now,
            Transactions = parsed.Transactions.ToList(),
        };

        dbContext.BillingImports.Add(import);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            dbContext.ChangeTracker.Clear();
            var concurrentImport = await dbContext.BillingImports
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.ContentSha256 == contentSha256, cancellationToken);
            if (concurrentImport is null)
                throw;

            throw Duplicate(concurrentImport);
        }

        return new BillingImportResponse(
            import.Id,
            import.OriginalFileName,
            import.Status,
            import.TransactionCount,
            parsed.RowsWithoutProductName,
            import.CreatedAt);
    }

    private static InvalidPosWorkbookException InvalidFile(string message) =>
        new([new BillingImportValidationIssue(null, "File", message)]);

    private static DuplicateBillingImportException Duplicate(BillingImport import) =>
        new(new BillingImportDuplicateResponse(
            "This exact workbook has already been imported.",
            import.Id,
            import.OriginalFileName,
            import.Status,
            import.CreatedAt));

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        };
}

public sealed class DuplicateBillingImportException(BillingImportDuplicateResponse response)
    : Exception("This exact workbook has already been imported.")
{
    public BillingImportDuplicateResponse Response { get; } = response;
}
