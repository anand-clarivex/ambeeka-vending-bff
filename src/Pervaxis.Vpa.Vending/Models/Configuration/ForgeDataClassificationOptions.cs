namespace Pervaxis.Vpa.Vending.Models.Configuration;

public sealed class ForgeDataClassificationOptions
{
    public const string SectionName = "Forge:DataClassification";

    public string DefaultClassification { get; init; } = "internal";

    public string[] SensitiveKeys { get; init; } = ["password", "secret", "token", "apiKey", "email", "phone"];
}
