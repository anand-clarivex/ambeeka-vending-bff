
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

using AWS.Logger.SeriLog;
using Pervaxis.Vpa.Vending.Models.Configuration;
using Serilog;
using Serilog.Enrichers.Span;
using Serilog.Formatting.Compact;

namespace Pervaxis.Vpa.Vending.Infrastructure.Extensions;

public static class SerilogExtensions
{
    public static WebApplicationBuilder AddForgeSerilog(this WebApplicationBuilder builder)
    {
        builder.Host.UseSerilog((context, loggerConfig) =>
        {
            var options = new ForgeLoggingOptions();
            context.Configuration.GetSection(ForgeLoggingOptions.SectionName).Bind(options);

            loggerConfig
                .ReadFrom.Configuration(context.Configuration)
                .Enrich.WithProperty("ServiceName", options.ServiceName)
                .Enrich.WithProperty("Environment", context.HostingEnvironment.EnvironmentName)
                .Enrich.WithMachineName()
                .Enrich.FromLogContext()
                .Enrich.WithSpan();

            var isRunningInEcs = !string.IsNullOrWhiteSpace(
                Environment.GetEnvironmentVariable("ECS_CONTAINER_METADATA_URI_V4"));

            if (context.HostingEnvironment.IsDevelopment() || isRunningInEcs)
            {
                loggerConfig.WriteTo.Console(new RenderedCompactJsonFormatter());
            }
            else
            {
                if (options.CloudWatch is not null)
                {
                    var awsConfig = new AWS.Logger.AWSLoggerConfig
                    {
                        LogGroup = options.CloudWatch.LogGroup,
                        LogStreamNamePrefix = options.CloudWatch.LogStreamPrefix ?? options.ServiceName
                    };

                    loggerConfig.WriteTo.AWSSeriLog(awsConfig, textFormatter: new RenderedCompactJsonFormatter());
                }
            }
        });

        return builder;
    }
}

