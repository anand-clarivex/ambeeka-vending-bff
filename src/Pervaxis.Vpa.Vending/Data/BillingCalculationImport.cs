namespace Pervaxis.Vpa.Vending.Data;

public sealed class BillingCalculationImport
{
    public Guid CalculationId { get; set; }

    public BillingCalculation Calculation { get; set; } = null!;

    public Guid BillingImportId { get; set; }

    public BillingImport BillingImport { get; set; } = null!;
}
