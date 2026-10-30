using Microsoft.EntityFrameworkCore;
using Pervaxis.Vpa.Vending.Data;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

public sealed class BillingPosCleanupService(VendingDbContext dbContext)
{
    public const string ConfirmationPhrase = "RESET POS BILLING DATA";

    private static readonly string[] PreservedRecords = ["vending_products", "all non-POS application data"];

    public async Task<BillingPosCleanupPreviewResponse> GetPreviewAsync(CancellationToken cancellationToken)
    {
        var counts = await GetCountsAsync(cancellationToken);
        return new BillingPosCleanupPreviewResponse(
            "All POS billing imports, imported transaction rows, calculation snapshots, and calculation-to-import links.",
            ConfirmationPhrase,
            counts,
            PreservedRecords);
    }

    public async Task<BillingPosCleanupResponse> ResetAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Exclude uploads and calculations while the preview data is being removed.
        await dbContext.Database.ExecuteSqlRawAsync(
            "LOCK TABLE public.billing_calculations IN ACCESS EXCLUSIVE MODE",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "LOCK TABLE public.billing_calculation_imports IN ACCESS EXCLUSIVE MODE",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "LOCK TABLE public.billing_import_transactions IN ACCESS EXCLUSIVE MODE",
            cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "LOCK TABLE public.billing_imports IN ACCESS EXCLUSIVE MODE",
            cancellationToken);

        var counts = await GetCountsAsync(cancellationToken);

        // Remove the restrictive join rows first, then the source and dependent data.
        await dbContext.BillingCalculationImports.ExecuteDeleteAsync(cancellationToken);
        await dbContext.BillingCalculations.ExecuteDeleteAsync(cancellationToken);
        await dbContext.BillingImportTransactions.ExecuteDeleteAsync(cancellationToken);
        await dbContext.BillingImports.ExecuteDeleteAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        return new BillingPosCleanupResponse(counts, PreservedRecords, DateTimeOffset.UtcNow);
    }

    private async Task<BillingPosCleanupCounts> GetCountsAsync(CancellationToken cancellationToken) =>
        new(
            await dbContext.BillingCalculations.CountAsync(cancellationToken),
            await dbContext.BillingCalculationImports.CountAsync(cancellationToken),
            await dbContext.BillingImports.CountAsync(cancellationToken),
            await dbContext.BillingImportTransactions.CountAsync(cancellationToken));
}
