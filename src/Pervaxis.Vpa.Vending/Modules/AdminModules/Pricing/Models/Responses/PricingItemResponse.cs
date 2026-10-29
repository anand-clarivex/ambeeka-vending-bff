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

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Pricing.Models.Responses;

/// <summary>A pricing item returned by the admin API.</summary>
/// <param name="Id">The pricing item identifier.</param>
/// <param name="SystemProductId">The unique system product identifier.</param>
/// <param name="SystemProductName">The name used by the vending system.</param>
/// <param name="VendPrice">The vend price.</param>
/// <param name="ProductName">The customer-facing product name.</param>
/// <param name="CreatedAt">The UTC creation timestamp.</param>
/// <param name="UpdatedAt">The UTC last-update timestamp.</param>
public sealed record PricingItemResponse(
    Guid Id,
    string SystemProductId,
    string SystemProductName,
    decimal VendPrice,
    string ProductName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
