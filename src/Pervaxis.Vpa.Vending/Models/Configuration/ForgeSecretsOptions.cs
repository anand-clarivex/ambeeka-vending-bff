namespace Pervaxis.Vpa.Vending.Models.Configuration;

public sealed class ForgeSecretsOptions
{
    public const string SectionName = "Forge:Secrets";

    public bool UseSecretsManager { get; init; }

    public string? SecretId { get; init; }

    public string ApiKeySecretKey { get; init; } = "Forge:Authentication:ApiKey";
}
