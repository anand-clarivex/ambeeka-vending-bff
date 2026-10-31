using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Controllers;

/// <summary>Reviews pricing matches across every imported POS row for an employer and month.</summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("/api/v1/admin/billing/pricing-review")]
public sealed class BillingPricingReviewController(BillingProductReviewService reviewService) : ControllerBase
{
    /// <summary>Returns all pricing review rows within the monthly calculation scope without saving a calculation.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(BillingMonthProductReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BillingMonthProductReviewResponse>> GetAsync(
        [FromQuery] string month,
        [FromQuery] string employer,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
            return BadRequest(new { message = "Month must use yyyy-MM format." });
        if (string.IsNullOrWhiteSpace(employer) || employer.Trim().Length > 255)
            return BadRequest(new { message = "Employer is required and must be 255 characters or fewer." });

        var response = await reviewService.GetMonthReviewAsync(
            monthDate.Year,
            monthDate.Month,
            employer,
            cancellationToken);
        return Ok(response);
    }
}
