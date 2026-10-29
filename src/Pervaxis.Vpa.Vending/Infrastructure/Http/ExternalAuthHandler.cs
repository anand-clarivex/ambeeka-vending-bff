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

using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Pervaxis.Vpa.Vending.Models.Configuration;

namespace Pervaxis.Vpa.Vending.Infrastructure.Http;

public sealed class ExternalAuthHandler : DelegatingHandler
{
    private readonly IOptionsMonitor<ForgeAuthenticationOptions> _authOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExternalAuthHandler(
        IOptionsMonitor<ForgeAuthenticationOptions> authOptions,
        IHttpContextAccessor httpContextAccessor)
    {
        _authOptions = authOptions;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var scheme = _authOptions.CurrentValue.Scheme;

        if (scheme is "Bearer" or "Both")
        {
            // Forward the inbound Bearer token to external services
            var token = _httpContextAccessor.HttpContext?.Items["BearerToken"] as string;
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        else if (scheme is "ApiKey")
        {
            // For API key auth, use the configured API key for external service calls
            var apiKey = _authOptions.CurrentValue.ApiKey;
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Api-Key", apiKey);
            }
        }

        return base.SendAsync(request, cancellationToken);
    }
}
