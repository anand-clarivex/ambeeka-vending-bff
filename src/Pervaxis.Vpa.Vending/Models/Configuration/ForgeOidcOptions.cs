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

namespace Pervaxis.Vpa.Vending.Models.Configuration;

public sealed class ForgeOidcOptions
{
    public const string SectionName = "Forge:Oidc";

    /// <summary>
    /// The OIDC provider in use. Supported values: Auth0, Supabase, Okta, Cognito.
    /// </summary>
    public string Provider { get; init; } = "Auth0";

    public string? Authority { get; set; }

    public string? Audience { get; set; }

    /// <summary>
    /// Retained for Auth0 management API compatibility.
    /// </summary>
    public string? Domain { get; set; }
}
