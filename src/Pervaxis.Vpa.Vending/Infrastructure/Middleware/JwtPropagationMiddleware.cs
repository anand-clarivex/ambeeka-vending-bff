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

namespace Pervaxis.Vpa.Vending.Infrastructure.Middleware;

public sealed class JwtPropagationMiddleware
{
    private const string BearerPrefix = "Bearer ";
    private const string ItemKey = "BearerToken";

    private readonly RequestDelegate _next;
    private readonly ILogger<JwtPropagationMiddleware> _logger;

    public JwtPropagationMiddleware(RequestDelegate next, ILogger<JwtPropagationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

        if (!string.IsNullOrEmpty(authHeader) &&
            authHeader.StartsWith(BearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader[BearerPrefix.Length..];
            context.Items[ItemKey] = token;
            _logger.LogDebug("Bearer token extracted and stored in HttpContext.Items");
        }
        else
        {
            _logger.LogDebug("No Bearer token found in Authorization header");
        }

        await _next(context).ConfigureAwait(false);
    }
}
