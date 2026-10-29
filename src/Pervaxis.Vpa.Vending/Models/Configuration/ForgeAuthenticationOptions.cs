namespace Pervaxis.Vpa.Vending.Models.Configuration;

public sealed class ForgeAuthenticationOptions
{
    public const string SectionName = "Forge:Authentication";

    public string? ApiKey { get; init; }

    /// <summary>Controls which auth scheme(s) are active. Accepted values: "ApiKey", "Bearer", "Both".</summary>
    public string Scheme { get; init; } = "ApiKey";
}
