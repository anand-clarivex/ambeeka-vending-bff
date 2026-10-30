namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingImportValidationIssue(
    int? RowNumber,
    string Field,
    string Message);
