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

using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Pervaxis.Vpa.Vending.Infrastructure.Exceptions;
using Pervaxis.Vpa.Vending.Models.Configuration;
using Pervaxis.Vpa.Vending.Services;
using Polly.CircuitBreaker;

namespace Pervaxis.Vpa.Vending.Infrastructure.Middleware;

public sealed class ExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IOptions<ForgeResilienceOptions> _resilienceOptions;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlerMiddleware> logger,
        IHostEnvironment environment,
        IOptions<ForgeResilienceOptions> resilienceOptions)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
        _resilienceOptions = resilienceOptions;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            if (context.Response.HasStarted)
            {
                throw;
            }

            var errorId = Guid.NewGuid().ToString();
            var correlationId = context.Items["CorrelationId"]?.ToString() ?? string.Empty;

            var problemDetails = MapException(ex, errorId, correlationId);

            LogException(ex, errorId, correlationId, problemDetails.Status ?? 500);

            if (ex is BrokenCircuitException)
            {
                context.Response.Headers["Retry-After"] =
                    _resilienceOptions.Value.CircuitBreakerBreakDurationSeconds.ToString();
            }

            context.Response.Clear();
            context.Response.StatusCode = problemDetails.Status ?? 500;
            context.Response.ContentType = "application/problem+json";

            var responseBody = new
            {
                type = problemDetails.Type,
                title = problemDetails.Title,
                status = problemDetails.Status,
                detail = problemDetails.Detail,
                errorId,
                correlationId
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(responseBody, JsonOptions)).ConfigureAwait(false);
        }
    }

    private ProblemDetails MapException(Exception ex, string errorId, string correlationId)
    {
        return ex switch
        {
            FluentValidation.ValidationException fluentEx => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Validation Error",
                Status = StatusCodes.Status400BadRequest,
                Detail = string.Join(" ", fluentEx.Errors.Select(e => e.ErrorMessage))
            },

            SlugConflictException conflictEx => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Conflict",
                Status = StatusCodes.Status409Conflict,
                Detail = conflictEx.Message
            },

            OperationCanceledException => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Client Disconnected",
                Status = 499,
                Detail = "Client disconnected"
            },

            BrokenCircuitException => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Service Unavailable",
                Status = StatusCodes.Status503ServiceUnavailable,
                Detail = "Service temporarily unavailable"
            },

            _ => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7807",
                Title = "Internal Server Error",
                Status = StatusCodes.Status500InternalServerError,
                Detail = _environment.IsDevelopment()
                    ? $"{ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}"
                    : "An unexpected error occurred."
            }
        };
    }

    private void LogException(Exception ex, string errorId, string correlationId, int statusCode)
    {
        switch (statusCode)
        {
            case 400:
                _logger.LogInformation(
                    ex,
                    "Validation error {ErrorId} {CorrelationId} {StatusCode}",
                    errorId, correlationId, statusCode);
                break;

            case 499:
                _logger.LogWarning(
                    ex,
                    "Client disconnected {ErrorId} {CorrelationId} {StatusCode}",
                    errorId, correlationId, statusCode);
                break;

            case 503:
                _logger.LogWarning(
                    ex,
                    "Circuit breaker open {ErrorId} {CorrelationId} {StatusCode}",
                    errorId, correlationId, statusCode);
                break;

            default:
                _logger.LogError(
                    ex,
                    "Unhandled exception {ErrorId} {CorrelationId} {StatusCode}",
                    errorId, correlationId, statusCode);
                break;
        }
    }
}
