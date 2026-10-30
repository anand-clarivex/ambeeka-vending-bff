namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingImportResponse(
    Guid Id,
    string FileName,
    string Status,
    int TransactionsImported,
    int RowsWithoutProductName,
    DateTimeOffset CreatedAt);
