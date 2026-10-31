namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingCalculationHistoryResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<BillingCalculationHistoryItem> Items);

public sealed record BillingCalculationHistoryItem(
    Guid Id,
    string CalculationName,
    string EmployerName,
    int Year,
    int Month,
    string Status,
    DateTimeOffset CreatedAt);
