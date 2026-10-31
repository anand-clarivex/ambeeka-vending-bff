using Microsoft.AspNetCore.Http;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;

public sealed class UploadBillingWorkbookRequest
{
    public IFormFile? File { get; init; }

    public string? DisplayName { get; init; }
}
