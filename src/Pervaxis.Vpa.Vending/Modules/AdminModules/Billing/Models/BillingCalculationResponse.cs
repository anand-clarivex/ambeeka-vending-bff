using Ambeeka.Calculation;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed record BillingCalculationResponse(
    Guid Id,
    string EmployerName,
    string Status,
    DateTimeOffset CreatedAt,
    MonthlyBillingResult Result);
