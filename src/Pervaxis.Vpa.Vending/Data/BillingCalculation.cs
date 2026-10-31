namespace Pervaxis.Vpa.Vending.Data;

public sealed class BillingCalculation
{
    public Guid Id { get; set; }

    public string EmployerName { get; set; } = string.Empty;

    public string CalculationName { get; set; } = string.Empty;

    public int Year { get; set; }

    public int Month { get; set; }

    public string Status { get; set; } = "Preview";

    public string ResultJson { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public List<BillingCalculationImport> Imports { get; set; } = [];
}
