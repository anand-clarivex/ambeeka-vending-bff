using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Controllers;

/// <summary>Previews and resets POS billing POC data while preserving product pricing.</summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("/api/v1/admin/billing/pos-cleanup")]
public sealed class BillingPosCleanupController(BillingPosCleanupService cleanupService) : ControllerBase
{
    /// <summary>Returns the POS billing records that a reset would remove.</summary>
    [HttpGet("preview")]
    [ProducesResponseType(typeof(BillingPosCleanupPreviewResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BillingPosCleanupPreviewResponse>> GetPreviewAsync(
        CancellationToken cancellationToken)
    {
        var preview = await cleanupService.GetPreviewAsync(cancellationToken);
        return Ok(preview);
    }

    /// <summary>Deletes all POS billing imports and calculation snapshots.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BillingPosCleanupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BillingPosCleanupResponse>> ResetAsync(
        [FromBody] BillingPosCleanupRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                request.Confirmation,
                BillingPosCleanupService.ConfirmationPhrase,
                StringComparison.Ordinal))
        {
            return BadRequest(new
            {
                message = "Confirmation phrase does not match.",
                confirmationPhrase = BillingPosCleanupService.ConfirmationPhrase,
            });
        }

        var result = await cleanupService.ResetAsync(cancellationToken);
        return Ok(result);
    }
}
