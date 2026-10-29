
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

using System.Diagnostics;

namespace Pervaxis.Vpa.Vending.Infrastructure.Http;

public sealed class HttpLoggingHandler : DelegatingHandler
{
    private readonly ILogger<HttpLoggingHandler> _logger;
    private readonly IHttpContextAccessor _httpContextAccessor;

    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "X-Api-Key"
    };

    public HttpLoggingHandler(
        ILogger<HttpLoggingHandler> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var method = request.Method;
        var uri = request.RequestUri;
        var correlationId = _httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();

        using var scope = _logger.BeginScope(
            new Dictionary<string, object?>
            {
                ["CorrelationId"] = correlationId
            });

        _logger.LogInformation("HTTP {Method} {Uri}", method, uri);

        var startTimestamp = Stopwatch.GetTimestamp();

        try
        {
            var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);

            _logger.LogInformation(
                "HTTP {Method} {Uri} responded {StatusCode} in {ElapsedMs}ms",
                method,
                uri,
                (int)response.StatusCode,
                elapsed.TotalMilliseconds);

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var elapsed = Stopwatch.GetElapsedTime(startTimestamp);

            _logger.LogError(
                "HTTP {Method} {Uri} failed with {ExceptionType} in {ElapsedMs}ms",
                method,
                uri,
                ex.GetType().Name,
                elapsed.TotalMilliseconds);

            throw;
        }
    }

    internal static bool IsSensitiveHeader(string headerName)
    {
        if (SensitiveHeaders.Contains(headerName))
            return true;

        return headerName.EndsWith("-Token", StringComparison.OrdinalIgnoreCase);
    }
}

