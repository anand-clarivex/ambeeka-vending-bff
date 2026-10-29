using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Pervaxis.Vpa.Vending.Models.Configuration;

namespace Pervaxis.Vpa.Vending.Auth;

public sealed class ForgeApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly IAmazonSecretsManager _secretsManager;
    private readonly IOptionsMonitor<ForgeAuthenticationOptions> _authenticationOptions;
    private readonly IOptionsMonitor<ForgeSecretsOptions> _secretsOptions;

    public ForgeApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IAmazonSecretsManager secretsManager,
        IOptionsMonitor<ForgeAuthenticationOptions> authenticationOptions,
        IOptionsMonitor<ForgeSecretsOptions> secretsOptions)
        : base(options, logger, encoder)
    {
        _secretsManager = secretsManager;
        _authenticationOptions = authenticationOptions;
        _secretsOptions = secretsOptions;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
            string.IsNullOrWhiteSpace(providedKey))
        {
            return AuthenticateResult.NoResult();
        }

        var expectedKey = await ResolveApiKeyAsync();
        if (string.IsNullOrWhiteSpace(expectedKey) || !string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("Invalid API key.");
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "forge-api-client"),
            new Claim(ClaimTypes.Name, "Forge API Client"),
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    private async Task<string?> ResolveApiKeyAsync()
    {
        var directKey = _authenticationOptions.CurrentValue.ApiKey;
        if (!string.IsNullOrWhiteSpace(directKey))
        {
            return directKey;
        }

        var secrets = _secretsOptions.CurrentValue;
        if (!secrets.UseSecretsManager || string.IsNullOrWhiteSpace(secrets.SecretId))
        {
            return null;
        }

        var response = await _secretsManager.GetSecretValueAsync(new GetSecretValueRequest
        {
            SecretId = secrets.SecretId,
        });

        if (string.IsNullOrWhiteSpace(response.SecretString))
        {
            return null;
        }

        using var document = JsonDocument.Parse(response.SecretString);
        return document.RootElement.TryGetProperty(secrets.ApiKeySecretKey, out var value)
            ? value.GetString()
            : null;
    }
}
