
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

using Microsoft.Extensions.Http.Resilience;
using Pervaxis.Vpa.Vending.Models.Configuration;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace Pervaxis.Vpa.Vending.Infrastructure.Extensions;

public static class ResilienceExtensions
{
    public static IHttpClientBuilder AddForgeResilience(
        this IHttpClientBuilder builder,
        IConfiguration resilienceSection)
    {
        var clientOptions = new ForgeResilienceClientOptions();
        resilienceSection.Bind(clientOptions);

        var defaults = new ForgeResilienceOptions();

        var maxRetryAttempts = clientOptions.MaxRetryAttempts ?? defaults.MaxRetryAttempts;
        var failureThreshold = clientOptions.CircuitBreakerFailureThreshold ?? defaults.CircuitBreakerFailureThreshold;
        var samplingWindowSeconds = clientOptions.CircuitBreakerSamplingWindowSeconds ?? defaults.CircuitBreakerSamplingWindowSeconds;
        var breakDurationSeconds = clientOptions.CircuitBreakerBreakDurationSeconds ?? defaults.CircuitBreakerBreakDurationSeconds;
        var timeoutSeconds = clientOptions.TimeoutSeconds ?? defaults.TimeoutSeconds;

        builder.AddResilienceHandler("forge-resilience", (resilienceBuilder, context) =>
        {
            var logger = context.ServiceProvider.GetRequiredService<ILogger<HttpClient>>();

            resilienceBuilder
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = maxRetryAttempts,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .HandleResult(r => r.StatusCode >= System.Net.HttpStatusCode.InternalServerError
                            || r.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>()
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = failureThreshold / 100.0,
                    SamplingDuration = TimeSpan.FromSeconds(samplingWindowSeconds),
                    BreakDuration = TimeSpan.FromSeconds(breakDurationSeconds),
                    MinimumThroughput = failureThreshold,
                    ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                        .HandleResult(r => r.StatusCode >= System.Net.HttpStatusCode.InternalServerError)
                        .Handle<HttpRequestException>()
                        .Handle<TimeoutRejectedException>(),
                    OnOpened = args =>
                    {
                        logger.LogWarning(
                            "Circuit breaker opened. Break duration: {BreakDuration}s",
                            breakDurationSeconds);
                        return ValueTask.CompletedTask;
                    },
                    OnClosed = _ =>
                    {
                        logger.LogWarning("Circuit breaker closed. Requests flowing normally.");
                        return ValueTask.CompletedTask;
                    },
                    OnHalfOpened = _ =>
                    {
                        logger.LogWarning("Circuit breaker half-opened. Testing with next request.");
                        return ValueTask.CompletedTask;
                    }
                })
                .AddTimeout(new HttpTimeoutStrategyOptions
                {
                    Timeout = TimeSpan.FromSeconds(timeoutSeconds)
                });
        });

        return builder;
    }
}

