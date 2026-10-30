namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingPosCleanupRequest(string? Confirmation);

public sealed record BillingPosCleanupCounts(
    int Calculations,
    int CalculationImportLinks,
    int Imports,
    int Transactions);

public sealed record BillingPosCleanupPreviewResponse(
    string Scope,
    string ConfirmationPhrase,
    BillingPosCleanupCounts RecordsToDelete,
    IReadOnlyList<string> RecordsPreserved);

public sealed record BillingPosCleanupResponse(
    BillingPosCleanupCounts Deleted,
    IReadOnlyList<string> Preserved,
    DateTimeOffset CompletedAt);
