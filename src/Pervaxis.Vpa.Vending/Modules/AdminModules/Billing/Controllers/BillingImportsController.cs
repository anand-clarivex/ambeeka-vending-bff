using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pervaxis.Vpa.Vending.Data;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Controllers;

/// <summary>Receives and validates administrator-uploaded POS workbooks.</summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("/api/v1/admin/billing/imports")]
public sealed class BillingImportsController(
    BillingImportService importService,
    BillingProductReviewService productReviewService,
    VendingDbContext dbContext) : ControllerBase
{
    /// <summary>Lists imported workbook metadata, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(BillingImportHistoryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BillingImportHistoryResponse>> GetHistoryAsync(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Page must be at least 1 and pageSize must be between 1 and 100." });

        var imports = dbContext.BillingImports.AsNoTracking();
        var totalCount = await imports.CountAsync(cancellationToken);
        var items = await imports
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new BillingImportHistoryItem(
                item.Id,
                item.OriginalFileName,
                item.DisplayName,
                item.Status,
                item.TransactionCount,
                item.RowsWithoutProductName,
                item.CreatedAt,
                item.CalculatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new BillingImportHistoryResponse(page, pageSize, totalCount, items));
    }

    /// <summary>Rechecks an imported workbook against the current POS product aliases and prices.</summary>
    [HttpGet("{id:guid}/pricing-review")]
    [ProducesResponseType(typeof(BillingImportProductReviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BillingImportProductReviewResponse>> GetPricingReviewAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var review = await productReviewService.GetImportReviewAsync(id, cancellationToken);
        return review is null ? NotFound() : Ok(review);
    }

    /// <summary>Permanently deletes an import that has not been used by a billing calculation.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(BillingImportDeleteConflictResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var import = await dbContext.BillingImports
            .FromSqlInterpolated($"SELECT * FROM public.billing_imports WHERE id = {id} FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

        if (import is null)
            return NotFound();

        if (import.CalculatedAt is not null)
        {
            return Conflict(new BillingImportDeleteConflictResponse(
                "This import cannot be deleted because a billing calculation has used it.",
                import.Id,
                import.CalculatedAt.Value));
        }

        dbContext.BillingImports.Remove(import);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>Uploads a POS workbook, validates it, and stages the non-sensitive fields in PostgreSQL.</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(BillingImportService.MaximumUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = BillingImportService.MaximumUploadBytes)]
    [ProducesResponseType(typeof(BillingImportResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(BillingImportValidationResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BillingImportDuplicateResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<BillingImportResponse>> UploadAsync(
        [FromForm] UploadBillingWorkbookRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await importService.UploadAsync(request.File, request.DisplayName, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, response);
        }
        catch (DuplicateBillingImportException exception)
        {
            return Conflict(exception.Response);
        }
        catch (InvalidPosWorkbookException exception)
        {
            return BadRequest(new BillingImportValidationResponse(
                "The POS workbook could not be imported.",
                exception.Issues,
                exception.IgnoredRows));
        }
    }
}
