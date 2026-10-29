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

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Pervaxis.Vpa.Vending.Models.Configuration;
using Pervaxis.Vpa.Vending.Auth;

namespace Pervaxis.Vpa.Vending.Infrastructure.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddForgeAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var oidcOptions = new ForgeOidcOptions();
        configuration.GetSection(ForgeOidcOptions.SectionName).Bind(oidcOptions);

        var authOptions = new ForgeAuthenticationOptions();
        configuration.GetSection(ForgeAuthenticationOptions.SectionName).Bind(authOptions);

        var scheme = authOptions.Scheme?.Trim();
        if (scheme is not ("ApiKey" or "Bearer" or "Both"))
        {
            throw new InvalidOperationException(
                $"Configuration key '{ForgeAuthenticationOptions.SectionName}:Scheme' must be 'ApiKey', 'Bearer', or 'Both'.");
        }

        var isCognito = string.Equals(oidcOptions.Provider, "Cognito", StringComparison.OrdinalIgnoreCase);

        // Validate OIDC config only when JWT is needed
        if (scheme is "Bearer" or "Both")
        {
            if (string.IsNullOrEmpty(oidcOptions.Authority))
                throw new InvalidOperationException(
                    $"Configuration key '{ForgeOidcOptions.SectionName}:Authority' is required when Scheme is '{scheme}'.");

            if (string.IsNullOrEmpty(oidcOptions.Audience))
                throw new InvalidOperationException(
                    $"Configuration key '{ForgeOidcOptions.SectionName}:Audience' is required when Scheme is '{scheme}'.");
        }

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = scheme switch
            {
                "Bearer" => JwtBearerDefaults.AuthenticationScheme,
                "Both" => "ForgeMultiScheme",
                _ => "ForgeApiKey"
            };
            options.DefaultChallengeScheme = options.DefaultAuthenticateScheme;
        });

        // Always register ForgeApiKey scheme
        authBuilder.AddScheme<AuthenticationSchemeOptions, ForgeApiKeyAuthenticationHandler>(
            "ForgeApiKey", _ => { });

        // Register JwtBearer when needed
        if (scheme is "Bearer" or "Both")
        {
            authBuilder.AddJwtBearer(options =>
            {
                options.Authority = oidcOptions.Authority;
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    // Cognito access tokens identify the app client in `client_id`.
                    // Their `aud` claim is optional and is only present for resource binding.
                    ValidateAudience = !isCognito,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    NameClaimType = "username",
                };

                if (isCognito)
                {
                    options.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var tokenUse = context.Principal?.FindFirst("token_use")?.Value;
                            var clientId = context.Principal?.FindFirst("client_id")?.Value;
                            if (!string.Equals(tokenUse, "access", StringComparison.Ordinal) ||
                                !string.Equals(clientId, oidcOptions.Audience, StringComparison.Ordinal))
                            {
                                context.Fail("The Cognito token must be an access token issued to the configured app client.");
                            }

                            return Task.CompletedTask;
                        }
                    };
                }
                else
                {
                    options.Audience = oidcOptions.Audience;
                }
            });
        }

        // Register PolicyScheme for "Both" mode
        if (scheme is "Both")
        {
            authBuilder.AddPolicyScheme("ForgeMultiScheme", "ForgeMultiScheme", options =>
            {
                options.ForwardDefaultSelector = context =>
                {
                    var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
                    if (!string.IsNullOrEmpty(authHeader) &&
                        authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        return JwtBearerDefaults.AuthenticationScheme;
                    }
                    return "ForgeApiKey";
                };
            });
        }

        // Authorization policy
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .AddAuthenticationSchemes(scheme switch
                {
                    "Bearer" => JwtBearerDefaults.AuthenticationScheme,
                    "Both" => "ForgeMultiScheme",
                    _ => "ForgeApiKey"
                })
                .RequireAuthenticatedUser()
                .Build();

            var adminGroup = configuration["Forge:Authorization:AdminGroup"]?.Trim();
            options.AddPolicy("AdminOnly", policy => policy
                .RequireAuthenticatedUser()
                .RequireClaim("cognito:groups", string.IsNullOrEmpty(adminGroup) ? "Admin" : adminGroup));
        });

        // Add Swagger Bearer security definition when JWT is active
        if (scheme is "Bearer" or "Both")
        {
            services.ConfigureSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your JWT token"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });
        }

        return services;
    }
}
