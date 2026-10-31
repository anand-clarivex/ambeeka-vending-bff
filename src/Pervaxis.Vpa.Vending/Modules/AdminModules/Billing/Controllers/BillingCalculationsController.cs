using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Controllers;

/// <summary>Creates and retrieves immutable billing calculation previews.</summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("/api/v1/admin/billing/calculations")]
public sealed class BillingCalculationsController(BillingCalculationService calculationService) : ControllerBase
{
    /// <summary>Lists calculation snapshots for the overview selector, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(BillingCalculationHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BillingCalculationHistoryResponse>> GetHistoryAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Page must be at least 1 and pageSize must be between 1 and 100." });

        return Ok(await calculationService.GetHistoryAsync(page, pageSize, cancellationToken));
    }

    /// <summary>Calculates a monthly preview for an employer and stores the immutable result snapshot.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(BillingCalculationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BillingCalculationResolutionRequiredResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BillingCalculationResponse>> CreatePreviewAsync(
        [FromQuery] string month,
        [FromQuery] string employer,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] BillingCalculationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!DateOnly.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monthDate))
            return BadRequest(new { message = "Month must use yyyy-MM format." });
        if (string.IsNullOrWhiteSpace(employer) || employer.Trim().Length > 255)
            return BadRequest(new { message = "Employer is required and must be 255 characters or fewer." });
        if (request?.CalculationName?.Trim().Length > 255)
            return BadRequest(new { message = "Calculation name must be 255 characters or fewer." });

        BillingCalculationResponse? response;
        try
        {
            response = await calculationService.CreatePreviewAsync(
                monthDate.Year,
                monthDate.Month,
                employer,
                request?.CalculationName,
                request?.Resolutions,
                cancellationToken);
        }
        catch (BillingCalculationResolutionException exception)
        {
            return StatusCode(exception.StatusCode, exception.Response);
        }
        if (response is null)
            return NotFound(new { message = "No imported transactions were found for this employer and month." });

        return Created($"/api/v1/admin/billing/calculations/{response.Id}", response);
    }

    /// <summary>Retrieves a previously stored calculation snapshot.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(BillingCalculationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BillingCalculationResponse>> GetAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var response = await calculationService.GetAsync(id, cancellationToken);
        return response is null ? NotFound() : Ok(response);
    }
}
