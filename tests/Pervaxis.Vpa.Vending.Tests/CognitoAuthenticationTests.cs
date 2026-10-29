using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pervaxis.Vpa.Vending.Infrastructure.Extensions;
using Xunit;

namespace Pervaxis.Vpa.Vending.Tests;

public sealed class CognitoAuthenticationTests
{
    private const string ExpectedClientId = "test-app-client";

    [Fact]
    public async Task CognitoAccessTokenForConfiguredClientIsAccepted()
    {
        using var provider = CreateServices();
        var options = GetJwtBearerOptions(provider);

        var result = await ValidateClaimsAsync(options,
            new Claim("token_use", "access"),
            new Claim("client_id", ExpectedClientId));

        Assert.Null(result);
        Assert.True(options.TokenValidationParameters.ValidateIssuer);
        Assert.False(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateLifetime);
        Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
    }

    [Theory]
    [InlineData("id", ExpectedClientId)]
    [InlineData("access", "another-app-client")]
    public async Task CognitoTokenWithWrongTypeOrClientIsRejected(string tokenUse, string clientId)
    {
        using var provider = CreateServices();
        var options = GetJwtBearerOptions(provider);

        var result = await ValidateClaimsAsync(options,
            new Claim("token_use", tokenUse),
            new Claim("client_id", clientId));

        Assert.NotNull(result?.Failure);
    }

    private static ServiceProvider CreateServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Forge:Authentication:Scheme"] = "Bearer",
                ["Forge:Oidc:Provider"] = "Cognito",
                ["Forge:Oidc:Authority"] = "https://cognito-idp.us-east-1.amazonaws.com/us-east-1_testPool",
                ["Forge:Oidc:Audience"] = ExpectedClientId,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddForgeAuthentication(configuration);
        return services.BuildServiceProvider();
    }

    private static JwtBearerOptions GetJwtBearerOptions(IServiceProvider services) =>
        services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);

    private static async Task<AuthenticateResult?> ValidateClaimsAsync(
        JwtBearerOptions options,
        params Claim[] claims)
    {
        var scheme = new AuthenticationScheme(
            JwtBearerDefaults.AuthenticationScheme,
            JwtBearerDefaults.AuthenticationScheme,
            typeof(JwtBearerHandler));
        var identity = new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme);
        var context = new TokenValidatedContext(
            new DefaultHttpContext(),
            scheme,
            options)
        {
            Principal = new ClaimsPrincipal(identity),
        };

        await options.Events.OnTokenValidated(context);
        return context.Result;
    }
}
