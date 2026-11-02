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
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Models;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Controllers;

/// <summary>Provides all-time weekly settlement metrics and recent imports for the admin dashboard.</summary>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("/api/v1/admin/billing/dashboard")]
public sealed class BillingDashboardController(BillingDashboardService dashboardService) : ControllerBase
{
    /// <summary>Returns dashboard metrics, weekly POS totals, and the ten most recent imports.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(BillingDashboardResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BillingDashboardResponse>> GetAsync(CancellationToken cancellationToken)
    {
        return Ok(await dashboardService.GetAsync(cancellationToken));
    }
}
