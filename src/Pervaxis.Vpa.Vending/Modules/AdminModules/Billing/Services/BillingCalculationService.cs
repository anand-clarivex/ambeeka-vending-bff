using System.Text.Json;
using System.Globalization;
using Ambeeka.Calculation;
using Microsoft.AspNetCore.Http;
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
        string? requestedCalculationName,
        IReadOnlyList<BillingCalculationRowResolution>? resolutions,
        CancellationToken cancellationToken)
    {
        var monthStart = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var nextMonth = monthStart.AddMonths(1);
        var normalizedEmployerName = employerName.Trim();
        var calculationName = string.IsNullOrWhiteSpace(requestedCalculationName)
            ? BuildDefaultCalculationName(normalizedEmployerName, year, month)
            : requestedCalculationName.Trim();

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
            .OrderBy(item => item.SourceRowNumber ?? int.MaxValue)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        var productPrices = BuildUnversionedPreviewPrices(products);
        var review = BillingProductReviewService.Analyze(sourceRows.First().BillingImportId, sourceRows, products);
        var issueByRow = review.Issues.ToDictionary(item => (item.ImportId, item.SourceRowNumber));
        var resolutionList = resolutions ?? [];
        var resolutionByRow = new Dictionary<(Guid ImportId, int SourceRowNumber), BillingCalculationRowResolution>();

        foreach (var resolution in resolutionList)
        {
            var key = (resolution.ImportId, resolution.SourceRowNumber);
            if (!issueByRow.ContainsKey(key))
                throw ResolutionError(
                    StatusCodes.Status400BadRequest,
                    "A resolution refers to a row that does not need pricing review.",
                    [],
                    review.AvailableProducts);
            if (!resolutionByRow.TryAdd(key, resolution))
                throw ResolutionError(
                    StatusCodes.Status400BadRequest,
                    "A row may have only one resolution.",
                    [issueByRow[key]],
                    review.AvailableProducts);

            if (string.Equals(resolution.Action, "Map", StringComparison.OrdinalIgnoreCase))
            {
                if (resolution.ProductId is null
                    || products.All(product => product.Id != resolution.ProductId.Value || product.VendPrice <= 0m))
                {
                    throw ResolutionError(
                        StatusCodes.Status400BadRequest,
                        "Map resolutions require a catalog product with a positive price.",
                        [issueByRow[key]],
                        review.AvailableProducts);
                }
            }
            else if (string.Equals(resolution.Action, "Ignore", StringComparison.OrdinalIgnoreCase))
            {
                if (resolution.ProductId is not null)
                    throw ResolutionError(
                        StatusCodes.Status400BadRequest,
                        "Ignore resolutions must not include a productId.",
                        [issueByRow[key]],
                        review.AvailableProducts);
            }
            else
            {
                throw ResolutionError(
                    StatusCodes.Status400BadRequest,
                    "Resolution action must be Map or Ignore.",
                    [issueByRow[key]],
                    review.AvailableProducts);
            }
        }

        var unresolvedIssues = review.Issues
            .Where(issue => !resolutionByRow.ContainsKey((issue.ImportId, issue.SourceRowNumber)))
            .ToArray();
        if (unresolvedIssues.Length > 0)
            throw ResolutionError(
                StatusCodes.Status422UnprocessableEntity,
                "Map or ignore every pricing review row before creating the calculation.",
                unresolvedIssues,
                review.AvailableProducts);

        var productById = products.ToDictionary(item => item.Id);
        var inputRows = new List<BillingTransactionInput>(sourceRows.Count);
        var appliedResolutions = new List<AppliedBillingResolution>();
        var ignoredTransactions = new List<IgnoredBillingTransaction>();
        foreach (var item in sourceRows)
        {
            var key = (item.BillingImportId, item.SourceRowNumber);
            if (!issueByRow.TryGetValue(key, out var issue))
            {
                inputRows.Add(new BillingTransactionInput(
                    item.TransactionId,
                    item.MachineSettlementTime,
                    item.ProductSelectionInfo,
                    item.SettlementValue,
                    item.BillingImportId,
                    item.SourceRowNumber));
                continue;
            }

            var resolution = resolutionByRow[key];
            if (string.Equals(resolution.Action, "Ignore", StringComparison.OrdinalIgnoreCase))
            {
                ignoredTransactions.Add(new IgnoredBillingTransaction(
                    item.BillingImportId,
                    item.SourceRowNumber,
                    item.TransactionId,
                    item.MachineSettlementTime,
                    item.SettlementValue,
                    item.ProductSelectionInfo));
                appliedResolutions.Add(new AppliedBillingResolution(
                    item.BillingImportId,
                    item.SourceRowNumber,
                    item.TransactionId,
                    "Ignore",
                    null,
                    null,
                    item.ProductSelectionInfo));
                continue;
            }

            var product = productById[resolution.ProductId!.Value];
            inputRows.Add(new BillingTransactionInput(
                item.TransactionId,
                item.MachineSettlementTime,
                item.ProductSelectionInfo,
                item.SettlementValue,
                item.BillingImportId,
                item.SourceRowNumber,
                new ProductPriceAlias(product.ProductName, product.SystemProductName, product.VendPrice, DateOnly.MinValue)));
            appliedResolutions.Add(new AppliedBillingResolution(
                item.BillingImportId,
                item.SourceRowNumber,
                item.TransactionId,
                "Map",
                product.Id,
                product.ProductName,
                item.ProductSelectionInfo));
        }

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
            }) with
        {
            AppliedResolutions = appliedResolutions,
            IgnoredTransactions = ignoredTransactions,
        };

        var createdAt = DateTimeOffset.UtcNow;
        foreach (var import in imports)
            import.CalculatedAt ??= createdAt;

        var calculation = new BillingCalculation
        {
            Id = Guid.NewGuid(),
            EmployerName = normalizedEmployerName,
            CalculationName = calculationName,
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
            calculation.CalculationName,
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
            calculation.CalculationName,
            calculation.EmployerName,
            calculation.Status,
            calculation.CreatedAt,
            result);
    }

    public async Task<BillingCalculationHistoryResponse> GetHistoryAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var calculations = dbContext.BillingCalculations.AsNoTracking();
        var totalCount = await calculations.CountAsync(cancellationToken);
        var items = await calculations
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new BillingCalculationHistoryItem(
                item.Id,
                item.CalculationName,
                item.EmployerName,
                item.Year,
                item.Month,
                item.Status,
                item.CreatedAt))
            .ToListAsync(cancellationToken);

        return new BillingCalculationHistoryResponse(page, pageSize, totalCount, items);
    }

    private static string BuildDefaultCalculationName(string employerName, int year, int month)
    {
        var suffix = $" · {new DateTime(year, month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture)}";
        var employerNameLength = Math.Max(0, 255 - suffix.Length);
        var safeEmployerName = employerName.Length <= employerNameLength
            ? employerName
            : employerName[..employerNameLength].TrimEnd();
        return safeEmployerName + suffix;
    }

    private static IEnumerable<ProductPriceAlias> BuildUnversionedPreviewPrices(
        IEnumerable<VendingProduct> currentCatalog)
    {
        // TODO: Replace this preview fallback with effective-dated price/alias versions before allowing billing finalization.
        return currentCatalog
            .Where(item => !string.IsNullOrWhiteSpace(item.ProductName))
            .GroupBy(item => item.ProductName, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Select(item => new ProductPriceAlias(
                item.ProductName,
                item.SystemProductName,
                item.VendPrice,
                DateOnly.MinValue));
    }

    private static BillingCalculationResolutionException ResolutionError(
        int statusCode,
        string message,
        IReadOnlyList<BillingProductReviewIssue> issues,
        IReadOnlyList<BillingProductOption> availableProducts) =>
        new(statusCode, new BillingCalculationResolutionRequiredResponse(message, issues, availableProducts));
}

public sealed class BillingCalculationResolutionException(
    int statusCode,
    BillingCalculationResolutionRequiredResponse response) : Exception(response.Message)
{
    public int StatusCode { get; } = statusCode;

    public BillingCalculationResolutionRequiredResponse Response { get; } = response;
}
