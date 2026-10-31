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

using Pervaxis.Vpa.Vending.Infrastructure.Extensions;
using Pervaxis.Vpa.Vending.Infrastructure.Http;
using Pervaxis.Vpa.Vending.Services;
using Pervaxis.Vpa.Vending.Modules.AdminModules.Billing.Services;
using Ambeeka.Calculation;

namespace Pervaxis.Vpa.Vending.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVendingServiceServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<IVendingService, VendingServiceService>();
        services.AddScoped<PosWorkbookReader>();
        services.AddScoped<BillingProductReviewService>();
        services.AddScoped<BillingImportService>();
        services.AddSingleton<BillingCalculationEngine>();
        services.AddScoped<BillingCalculationService>();
        services.AddScoped<BillingPosCleanupService>();

        // HTTP client with full DelegatingHandler chain:
        // CorrelationId → JwtPropagation → HttpLogging → Resilience (retry + circuit breaker + timeout)
        services.AddHttpClient<IForgeInternalHttpClient, ForgeInternalHttpClient>()
            .AddHttpMessageHandler<CorrelationIdHandler>()
            .AddHttpMessageHandler<JwtPropagationHandler>()
            .AddHttpMessageHandler<HttpLoggingHandler>()
            .AddForgeResilience(configuration.GetSection("Resilience:Default"));

        return services;
    }
}
