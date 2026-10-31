namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingProductOption(
    Guid ProductId,
    string PosAlias,
    string ProductName,
    decimal Price);

public sealed record BillingProductReviewIssue(
    Guid ImportId,
    int SourceRowNumber,
    string TransactionId,
    string? MachineName,
    DateTime MachineSettlementTime,
    decimal SettlementValue,
    string? ProductSelectionInfo,
    string ProductAlias,
    string Reason,
    IReadOnlyList<BillingProductOption> SuggestedProducts);

public sealed record BillingImportProductReviewResponse(
    Guid ImportId,
    int TransactionsReviewed,
    int RowsNeedingReview,
    IReadOnlyList<BillingProductReviewIssue> Issues,
    IReadOnlyList<BillingProductOption> AvailableProducts,
    string PriceBasis,
    string PricingWarning);

public sealed record BillingMonthProductReviewResponse(
    string Month,
    string Employer,
    int TransactionsReviewed,
    int RowsNeedingReview,
    IReadOnlyList<BillingProductReviewIssue> Issues,
    IReadOnlyList<BillingProductOption> AvailableProducts,
    string PriceBasis,
    string PricingWarning);

public sealed record BillingCalculationRowResolution(
    Guid ImportId,
    int SourceRowNumber,
    string Action,
    Guid? ProductId = null);

public sealed record BillingCalculationRequest(
    IReadOnlyList<BillingCalculationRowResolution>? Resolutions = null,
    string? CalculationName = null);

public sealed record BillingCalculationResolutionRequiredResponse(
    string Message,
    IReadOnlyList<BillingProductReviewIssue> Issues,
    IReadOnlyList<BillingProductOption> AvailableProducts);
