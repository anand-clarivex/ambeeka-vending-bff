namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingImportDeleteConflictResponse(
    string Message,
    Guid ImportId,
    DateTimeOffset CalculatedAt);
