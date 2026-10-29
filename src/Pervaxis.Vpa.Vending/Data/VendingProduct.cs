namespace Pervaxis.Vpa.Vending.Data;

public sealed class VendingProduct
{
    public Guid Id { get; set; }

    public string SystemProductId { get; set; } = string.Empty;

    public string SystemProductName { get; set; } = string.Empty;

    public decimal VendPrice { get; set; }

    public string ProductName { get; set; } = string.Empty;

    public string? SourceFile { get; set; }

    public int? SourceRowNumber { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
