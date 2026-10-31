namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingImportHistoryResponse(
    int Page,
    int PageSize,
    int TotalCount,
    IReadOnlyList<BillingImportHistoryItem> Items);

public sealed record BillingImportHistoryItem(
    Guid Id,
    string FileName,
    string DisplayName,
    string Status,
    int TransactionsImported,
    int? RowsWithoutProductName,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CalculatedAt);
