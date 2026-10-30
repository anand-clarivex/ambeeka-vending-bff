namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingImportDuplicateResponse(
    string Message,
    Guid ExistingImportId,
    string FileName,
    string Status,
    DateTimeOffset CreatedAt);
