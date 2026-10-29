
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

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Pervaxis.Vpa.Vending.Models.Configuration;

namespace Pervaxis.Vpa.Vending.Infrastructure.Extensions;

public static class TracingExtensions
{
    public static IServiceCollection AddForgeTracing(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = new ForgeTracingOptions();
        configuration.GetSection(ForgeTracingOptions.SectionName).Bind(options);

        var serviceName = configuration["Forge:ServiceName"] ?? "vending-service";

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddEntityFrameworkCoreInstrumentation()
                    .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(options.OtlpEndpoint));

                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    tracing.AddConsoleExporter();
                }
            });

        return services;
    }
}

