namespace Pervaxis.Vpa.Vending.Data;

public sealed class BillingImportTransaction
{
    public Guid Id { get; set; }

    public Guid BillingImportId { get; set; }

    public BillingImport BillingImport { get; set; } = null!;

    public int SourceRowNumber { get; set; }

    public string TransactionId { get; set; } = string.Empty;

    public string? SiteId { get; set; }

    public string? Currency { get; set; }

    public string? MachineName { get; set; }

    public decimal SettlementValue { get; set; }

    // Keep the POS wall-clock value as provided. Its timezone is still awaiting confirmation.
    public DateTime MachineSettlementTime { get; set; }

    public string? ProductSelectionInfo { get; set; }
}
