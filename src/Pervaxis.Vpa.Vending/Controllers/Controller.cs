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

using Microsoft.AspNetCore.Mvc;
using Pervaxis.Vpa.Vending.Models;
using Pervaxis.Vpa.Vending.Services;

namespace Pervaxis.Vpa.Vending.Controllers;

[ApiController]
[Route("/api/v1/vending")]
public sealed class VendingServiceController : ControllerBase
{
    private readonly IVendingService service;

    public VendingServiceController(IVendingService service)
    {
        this.service = service;
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(VendingServiceResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAsync(string id, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(id, cancellationToken);
        if (result is null)
            return NotFound();

        return Ok(result);
    }
}
