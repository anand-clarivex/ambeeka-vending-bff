using System.Text.Json;
using Ambeeka.Calculation;
using Microsoft.EntityFrameworkCore;
using Pervaxis.Vpa.Vending.Data;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

public sealed class BillingCalculationService(
    VendingDbContext dbContext,
    BillingCalculationEngine calculationEngine)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BillingCalculationResponse?> CreatePreviewAsync(
        int year,
        int month,
        string employerName,
        CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var nextMonth = monthStart.AddMonths(1);
        var normalizedEmployerName = employerName.Trim();

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var candidateImportIds = await dbContext.BillingImportTransactions
            .AsNoTracking()
            .Where(item => item.MachineName == normalizedEmployerName
                && item.MachineSettlementTime >= monthStart
                && item.MachineSettlementTime < nextMonth)
            .Select(item => item.BillingImportId)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(cancellationToken);
        if (candidateImportIds.Count == 0)
            return null;

        // Lock source imports in stable order so DELETE cannot race a calculation snapshot.
        var imports = new List<BillingImport>(candidateImportIds.Count);
        foreach (var importId in candidateImportIds)
        {
            var import = await dbContext.BillingImports
                .FromSqlInterpolated($"SELECT * FROM public.billing_imports WHERE id = {importId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
            if (import is not null)
                imports.Add(import);
        }

        var importIds = imports.Select(item => item.Id).ToArray();
        if (importIds.Length == 0)
            return null;

        var sourceRows = await dbContext.BillingImportTransactions
            .AsNoTracking()
            .Where(item => importIds.Contains(item.BillingImportId)
                && item.MachineName == normalizedEmployerName
                && item.MachineSettlementTime >= monthStart
                && item.MachineSettlementTime < nextMonth)
            .OrderBy(item => item.MachineSettlementTime)
            .ThenBy(item => item.BillingImportId)
            .ThenBy(item => item.SourceRowNumber)
            .ToListAsync(cancellationToken);
        if (sourceRows.Count == 0)
            return null;

        var products = await dbContext.VendingProducts
            .AsNoTracking()
            .OrderBy(item => item.SystemProductName)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var productPrices = BuildUnversionedPreviewPrices(products);
        var inputRows = sourceRows.Select(item => new BillingTransactionInput(
            item.TransactionId,
            item.MachineSettlementTime,
            item.ProductSelectionInfo,
            item.SettlementValue,
            item.BillingImportId,
            item.SourceRowNumber));
        var blankProductRows = imports.Sum(item => item.RowsWithoutProductName ?? 0);
        var result = calculationEngine.CalculateMonth(
            year,
            month,
            inputRows,
            productPrices,
            new ZeroSettlementSubsidyPolicy(),
            new BillingCalculationOptions
            {
                // TODO: Keep these assumptions unconfirmed until client responses and effective-dated pricing are available.
                AliasComparison = ProductAliasComparison.OrdinalIgnoreCase,
                EffectiveDateBasis = PricingEffectiveDateBasis.WeekPeriodStart,
                AliasComparisonConfirmed = false,
                EffectiveDatePolicyConfirmed = false,
                PricingHistoryAvailable = false,
                SettlementTimeZoneConfirmed = false,
                CurrencyAndRoundingConfirmed = false,
            },
            blankProductRows);

        var createdAt = DateTimeOffset.UtcNow;
        foreach (var import in imports)
            import.CalculatedAt ??= createdAt;

        var calculation = new BillingCalculation
        {
            Id = Guid.NewGuid(),
            EmployerName = normalizedEmployerName,
            Year = year,
            Month = month,
            Status = result.CanFinalize ? "ReadyForReview" : "NeedsReview",
            ResultJson = JsonSerializer.Serialize(result, JsonOptions),
            CreatedAt = createdAt,
            Imports = imports.Select(item => new BillingCalculationImport
            {
                BillingImportId = item.Id,
                BillingImport = item,
            }).ToList(),
        };
        dbContext.BillingCalculations.Add(calculation);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new BillingCalculationResponse(
            calculation.Id,
            calculation.EmployerName,
            calculation.Status,
            calculation.CreatedAt,
            result);
    }

    public async Task<BillingCalculationResponse?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var calculation = await dbContext.BillingCalculations
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (calculation is null)
            return null;

        var result = JsonSerializer.Deserialize<MonthlyBillingResult>(calculation.ResultJson, JsonOptions)
            ?? throw new InvalidOperationException("The stored billing calculation result is invalid.");
        return new BillingCalculationResponse(
            calculation.Id,
            calculation.EmployerName,
            calculation.Status,
            calculation.CreatedAt,
            result);
    }

    private static IEnumerable<ProductPriceAlias> BuildUnversionedPreviewPrices(
        IEnumerable<VendingProduct> currentCatalog)
    {
        // TODO: Replace this preview fallback with effective-dated price/alias versions before allowing billing finalization.
        return currentCatalog.Select(item => new ProductPriceAlias(
            item.SystemProductName,
            item.ProductName,
            item.VendPrice,
            DateOnly.MinValue));
    }
}
