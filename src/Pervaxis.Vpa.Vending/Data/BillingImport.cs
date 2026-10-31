namespace Pervaxis.Vpa.Vending.Data;

public sealed class BillingImport
{
    public Guid Id { get; set; }

    public string OriginalFileName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string ContentSha256 { get; set; } = string.Empty;

    public string Status { get; set; } = "Validated";

    public int TransactionCount { get; set; }

    public int? RowsWithoutProductName { get; set; }

    /// <summary>Set when a completed billing calculation uses this import.</summary>
    public DateTimeOffset? CalculatedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<BillingImportTransaction> Transactions { get; set; } = [];
}
