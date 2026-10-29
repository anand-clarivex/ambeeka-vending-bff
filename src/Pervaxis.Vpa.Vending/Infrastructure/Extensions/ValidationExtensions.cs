
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

using FluentValidation;
using Pervaxis.Vpa.Vending.Infrastructure.Validation;

namespace Pervaxis.Vpa.Vending.Infrastructure.Extensions;

public static class ValidationExtensions
{
    public static IServiceCollection AddForgeValidation(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<Program>();
        services.AddScoped<ValidationEndpointFilter>();
        return services;
    }

    public static RouteGroupBuilder WithValidation(this RouteGroupBuilder group)
    {
        group.AddEndpointFilter<ValidationEndpointFilter>();
        return group;
    }
}

