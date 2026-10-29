/*
 ************************************************************************
 * Copyright (C) 2026 Clarivex Technologies Private Limited
 * All Rights Reserved.
 *
 * NOTICE: All intellectual and technical concepts contained
 * herein are proprietary to Clarivex Technologies Private Limited
 * and may be covered by Indian and Foreign Patents,
 * patents in process, and are protected by trade secret or
 * copyright law. Dissemination of this information or reproduction
 * of this material is strictly forbidden unless prior written
 * permission is obtained from Clarivex Technologies Private Limited.
 *
 * Product:   Pervaxis Platform
 * Website:   https://clarivex.tech
 ************************************************************************
 */

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Pricing.Models.Requests;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Pricing.Models.Responses;
using Pervaxis.Vpa.Vending.Data;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Pricing.Controllers;

/// <summary>Provides administrator CRUD operations for imported product pricing.</summary>
/// <param name="dbContext">The database context that stores vending products.</param>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("/api/v1/admin/pricing")]
[Route("/api/v1/vending/products")]
public sealed class PricingController(VendingDbContext dbContext) : ControllerBase
{
    /// <summary>Retrieves all pricing items, ordered by product name.</summary>
    /// <param name="cancellationToken">Cancels the database query.</param>
    /// <returns>The pricing items.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PricingItemResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PricingItemResponse>>> GetAllAsync(
        CancellationToken cancellationToken)
    {
        var items = (await dbContext.VendingProducts
            .AsNoTracking()
            .OrderBy(product => product.ProductName)
            .ToListAsync(cancellationToken))
            .Select(ToResponse)
            .ToList();

        return Ok(items);
    }

    /// <summary>Retrieves a pricing item by its identifier.</summary>
    /// <param name="id">The pricing item identifier.</param>
    /// <param name="cancellationToken">Cancels the database query.</param>
    /// <returns>The pricing item, or <see cref="NotFoundResult"/> when it does not exist.</returns>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PricingItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PricingItemResponse>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.VendingProducts
            .AsNoTracking()
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);

        return item is null ? NotFound() : Ok(ToResponse(item));
    }

    /// <summary>Creates a pricing item.</summary>
    /// <param name="request">The pricing item fields.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The created pricing item, or a conflict when its system product ID already exists.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(PricingItemResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PricingItemResponse>> CreateAsync(
        PricingItemRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var item = new VendingProduct
        {
            Id = Guid.NewGuid(),
            SystemProductId = request.SystemProductId.Trim(),
            SystemProductName = request.SystemProductName.Trim(),
            VendPrice = request.VendPrice,
            ProductName = request.ProductName.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };

        dbContext.VendingProducts.Add(item);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Conflict(new { message = "A pricing item with this System Product ID already exists." });
        }

        return Created($"/api/v1/admin/pricing/{item.Id}", ToResponse(item));
    }

    /// <summary>Updates an existing pricing item.</summary>
    /// <param name="id">The pricing item identifier.</param>
    /// <param name="request">The replacement pricing item fields.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>The updated pricing item, not found, or a conflict when its system product ID is already used.</returns>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PricingItemResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<PricingItemResponse>> UpdateAsync(
        Guid id,
        PricingItemRequest request,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.VendingProducts
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
        if (item is null)
            return NotFound();

        item.SystemProductId = request.SystemProductId.Trim();
        item.SystemProductName = request.SystemProductName.Trim();
        item.VendPrice = request.VendPrice;
        item.ProductName = request.ProductName.Trim();
        item.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            return Conflict(new { message = "A pricing item with this System Product ID already exists." });
        }

        return Ok(ToResponse(item));
    }

    /// <summary>Deletes a pricing item.</summary>
    /// <param name="id">The pricing item identifier.</param>
    /// <param name="cancellationToken">Cancels the database operation.</param>
    /// <returns>No content when deleted, or not found when the item does not exist.</returns>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await dbContext.VendingProducts
            .FirstOrDefaultAsync(product => product.Id == id, cancellationToken);
        if (item is null)
            return NotFound();

        dbContext.VendingProducts.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    private static PricingItemResponse ToResponse(VendingProduct item) => new(
        item.Id,
        item.SystemProductId,
        item.SystemProductName,
        item.VendPrice,
        item.ProductName,
        item.CreatedAt,
        item.UpdatedAt);

    private static bool IsUniqueViolation(DbUpdateException exception) =>
        exception.GetBaseException() is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
        };
}
