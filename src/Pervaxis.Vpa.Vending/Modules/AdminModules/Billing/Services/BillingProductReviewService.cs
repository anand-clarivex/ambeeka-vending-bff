using Microsoft.EntityFrameworkCore;
using Pervaxis.Vpa.Vending.Data;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

public sealed class BillingProductReviewService(VendingDbContext dbContext)
{
    public const string PriceBasis = "CurrentCatalog";
    public const string PricingWarning = "Prices use the current catalog. Transaction-date price history is not available.";

    public async Task<BillingImportProductReviewResponse?> GetImportReviewAsync(
        Guid importId,
        CancellationToken cancellationToken)
    {
        var exists = await dbContext.BillingImports
            .AsNoTracking()
            .AnyAsync(item => item.Id == importId, cancellationToken);
        if (!exists)
            return null;

        var transactions = await dbContext.BillingImportTransactions
            .AsNoTracking()
            .Where(item => item.BillingImportId == importId)
            .OrderBy(item => item.SourceRowNumber)
            .ToListAsync(cancellationToken);
        var products = await GetProductsAsync(cancellationToken);
        return Analyze(importId, transactions, products);
    }

    public async Task<BillingMonthProductReviewResponse> GetMonthReviewAsync(
        int year,
        int month,
        string employer,
        CancellationToken cancellationToken)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var end = start.AddMonths(1);
        var normalizedEmployer = employer.Trim();
        var transactions = await dbContext.BillingImportTransactions
            .AsNoTracking()
            .Where(item => item.MachineName == normalizedEmployer
                && item.MachineSettlementTime >= start
                && item.MachineSettlementTime < end)
            .OrderBy(item => item.MachineSettlementTime)
            .ThenBy(item => item.BillingImportId)
            .ThenBy(item => item.SourceRowNumber)
            .ToListAsync(cancellationToken);
        var products = await GetProductsAsync(cancellationToken);
        var review = Analyze(Guid.Empty, transactions, products);

        return new BillingMonthProductReviewResponse(
            $"{year:D4}-{month:D2}",
            normalizedEmployer,
            review.TransactionsReviewed,
            review.RowsNeedingReview,
            review.Issues,
            review.AvailableProducts,
            PriceBasis,
            PricingWarning);
    }

    public async Task<List<VendingProduct>> GetProductsAsync(CancellationToken cancellationToken) =>
        await dbContext.VendingProducts
            .AsNoTracking()
            .OrderBy(item => item.SourceRowNumber ?? int.MaxValue)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

    public static BillingImportProductReviewResponse Analyze(
        Guid importId,
        IEnumerable<BillingImportTransaction> transactions,
        IReadOnlyList<VendingProduct> products)
    {
        var rows = transactions.ToArray();
        var options = products
            .Where(item => item.VendPrice > 0m)
            .Select(ToOption)
            .ToArray();
        var issues = new List<BillingProductReviewIssue>();

        foreach (var transaction in rows)
        {
            var alias = ExtractAlias(transaction.ProductSelectionInfo);
            // ProductName is the lookup value; SystemProductName is the standardized return value.
            var matchingProduct = string.IsNullOrWhiteSpace(alias)
                ? null
                : products.FirstOrDefault(item => string.Equals(
                    item.ProductName,
                    alias,
                    StringComparison.OrdinalIgnoreCase));

            string? reason = null;
            IReadOnlyList<BillingProductOption> suggestions = [];
            if (string.IsNullOrWhiteSpace(alias))
            {
                reason = "MissingProductName";
            }
            else if (matchingProduct is null)
            {
                reason = "AliasNotFound";
            }
            else if (matchingProduct.VendPrice <= 0m)
            {
                reason = "InvalidPrice";
            }

            if (reason is null)
                continue;

            issues.Add(new BillingProductReviewIssue(
                transaction.BillingImportId == Guid.Empty ? importId : transaction.BillingImportId,
                transaction.SourceRowNumber,
                transaction.TransactionId,
                transaction.MachineName,
                transaction.MachineSettlementTime,
                transaction.SettlementValue,
                transaction.ProductSelectionInfo,
                alias,
                reason,
                suggestions));
        }

        return new BillingImportProductReviewResponse(
            importId,
            rows.Length,
            issues.Count,
            issues,
            options,
            PriceBasis,
            PricingWarning);
    }

    public static string ExtractAlias(string? productSelectionInfo)
    {
        if (string.IsNullOrWhiteSpace(productSelectionInfo))
            return string.Empty;

        var parenthesis = productSelectionInfo.IndexOf('(');
        return (parenthesis < 0 ? productSelectionInfo : productSelectionInfo[..parenthesis]).Trim();
    }

    public static BillingProductOption ToOption(VendingProduct product) =>
        new(product.Id, product.ProductName, product.SystemProductName, product.VendPrice);
}
