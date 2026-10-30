namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingImportValidationResponse(
    string Message,
    IReadOnlyList<BillingImportValidationIssue> Issues);
