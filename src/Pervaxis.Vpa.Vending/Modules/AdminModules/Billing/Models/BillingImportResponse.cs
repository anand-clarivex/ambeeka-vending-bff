namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingImportResponse(
    Guid Id,
    string FileName,
    string DisplayName,
    string Status,
    int TransactionsImported,
    int RowsWithoutProductName,
    DateTimeOffset CreatedAt,
    BillingImportProductReviewResponse PricingReview,
    IReadOnlyList<BillingImportIgnoredRow> IgnoredRows);
