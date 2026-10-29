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

using Pervaxis.Vpa.Vending.Extensions;
using Pervaxis.Vpa.Vending.Infrastructure.Extensions;
using Pervaxis.Vpa.Vending.Infrastructure.Http;
using Pervaxis.Vpa.Vending.Infrastructure.Middleware;
using Pervaxis.Vpa.Vending.Infrastructure.Security;
using Pervaxis.Vpa.Vending.Models.Configuration;
using Amazon.Extensions.NETCore.Setup;
using Amazon.SecretsManager;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Diagnostics;
using Pervaxis.Vpa.Vending.Auth;
using Pervaxis.Vpa.Vending.Services;
using Pervaxis.Vpa.Vending.Infrastructure.HealthChecks;
using Pervaxis.Vpa.Vending.Data;

var builder = WebApplication.CreateBuilder(args);

// -- Structured Logging (Serilog + CloudWatch) ------------------------------------------

builder.AddForgeSerilog();


builder.Services.AddVendingServiceServices(builder.Configuration);
var vendingDatabaseConnectionString = builder.Configuration.GetConnectionString("VendingDatabase");
builder.Services.AddDbContext<VendingDbContext>(options =>
{
    if (!string.IsNullOrWhiteSpace(vendingDatabaseConnectionString))
        options.UseNpgsql(vendingDatabaseConnectionString);
});
builder.Services.AddDefaultAWSOptions(builder.Configuration.GetAWSOptions());
builder.Services.AddAWSService<IAmazonSecretsManager>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// -- Configuration options --------------------------------------------------------
builder.Services.Configure<ForgeResilienceOptions>(
    builder.Configuration.GetSection(ForgeResilienceOptions.SectionName));
builder.Services.AddOptions<ForgeDataClassificationOptions>()
    .BindConfiguration(ForgeDataClassificationOptions.SectionName);
builder.Services.AddOptions<ForgeAuthenticationOptions>()
    .BindConfiguration(ForgeAuthenticationOptions.SectionName);
builder.Services.AddOptions<ForgeSecretsOptions>()
    .BindConfiguration(ForgeSecretsOptions.SectionName);
builder.Services.AddSingleton<ForgeDataRedaction>();
builder.Services.AddOptions<ForgeOutputCachingOptions>()
    .BindConfiguration(ForgeOutputCachingOptions.SectionName);
builder.Services.AddOutputCache();
const string ForgeUiCorsPolicy = "ForgeUi";
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Forge:Cors:AllowedOrigins")
        .Get<string[]>() ?? ["http://localhost:4200"];

    options.AddPolicy(ForgeUiCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});
// -- Authentication (API Key + optional JWT/OIDC) ------------------------------------
builder.Services.AddForgeAuthentication(builder.Configuration);
builder.Services.Configure<ForgeOidcOptions>(builder.Configuration.GetSection(ForgeOidcOptions.SectionName));

// -- OpenTelemetry Tracing --------------------------------------------------------

builder.Services.AddForgeTracing(builder.Configuration);


// -- FluentValidation -------------------------------------------------------------

builder.Services.AddForgeValidation();


// -- API Versioning ---------------------------------------------------------------
builder.Services.AddForgeVersioning();

// -- HTTP Context + DelegatingHandlers --------------------------------------------
builder.Services.AddHttpContextAccessor();

builder.Services.AddTransient<CorrelationIdHandler>();


builder.Services.AddTransient<HttpLoggingHandler>();

builder.Services.AddTransient<ExternalAuthHandler>();
builder.Services.AddSingleton<IForgeHashingService, ForgeHashingService>();

builder.Services.AddHttpClient<IForgeExternalHttpClient, ForgeExternalHttpClient>(client =>
{
    var baseUrl = builder.Configuration["ExternalServices:BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
        client.BaseAddress = new Uri(baseUrl);
})
.AddHttpMessageHandler<CorrelationIdHandler>()
.AddHttpMessageHandler<ExternalAuthHandler>()
.AddHttpMessageHandler<HttpLoggingHandler>()
.AddForgeResilience(builder.Configuration.GetSection("Resilience:External"));

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "VendingService API",
        Version = "v1",
        Description = "Versioned REST API for the vpa vending-service service.",
    });
});
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);

var app = builder.Build();

// -- Middleware pipeline (order is critical) ------------------------------------------
// 1. Correlation ID first -- establishes request identity for all downstream

app.UseMiddleware<CorrelationIdMiddleware>();

// 2. JWT propagation -- extracts Bearer token before downstream middleware runs
app.UseMiddleware<JwtPropagationMiddleware>();
// 3. Exception handler third -- catches errors from all downstream middleware
app.UseMiddleware<ExceptionHandlerMiddleware>();
// 4. Security headers -- applies to all responses including error responses
app.UseMiddleware<SecurityHeadersMiddleware>();
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Forge:EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "VendingService API v1");
    });
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseOutputCache();
app.Use(async (context, next) =>
{
    var startedAt = Stopwatch.GetTimestamp();
    await next();

    var elapsedMs = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
    var actor = context.User.Identity?.IsAuthenticated == true
        ? context.User.Identity?.Name ?? "authenticated-user"
        : "anonymous";

    app.Logger.LogInformation(
        "Audit event {AuditAction} {Method} {Path} {StatusCode} {ElapsedMs}ms {Actor} {CorrelationId}",
        "request",
        context.Request.Method,
        context.Request.Path.Value,
        context.Response.StatusCode,
        elapsedMs,
        actor,
        context.Items["CorrelationId"]?.ToString() ?? context.TraceIdentifier);
});
app.UseCors(ForgeUiCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();
// Liveness stays independent of the database so local startup and container
// liveness remain useful while PostgreSQL credentials are being configured.
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => !check.Tags.Contains("ready"),
}).AllowAnonymous();

// Readiness performs a real PostgreSQL connection and SELECT 1.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
}).AllowAnonymous();
app.MapControllers().RequireAuthorization();

app.Run();

