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

using System.ComponentModel.DataAnnotations;

namespace Pervaxis.Vpa.Vending.Modules.AdminModules.Pricing.Models.Requests;

/// <summary>Input fields for creating or replacing a pricing item.</summary>
public sealed record PricingItemRequest
{
    /// <summary>Gets the unique system product identifier.</summary>
    [Required, MaxLength(64)]
    public string SystemProductId { get; init; } = string.Empty;

    /// <summary>Gets the name used by the vending system.</summary>
    [Required, MaxLength(255)]
    public string SystemProductName { get; init; } = string.Empty;

    /// <summary>Gets the vend price, in the configured currency.</summary>
    [Range(typeof(decimal), "0.01", "1000000.00")]
    public decimal VendPrice { get; init; }

    /// <summary>Gets the customer-facing product name.</summary>
    [Required, MaxLength(255)]
    public string ProductName { get; init; } = string.Empty;
}
