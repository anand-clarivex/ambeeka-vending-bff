namespace Pervaxis.Vpa.Vending.Models.Configuration;

public sealed class ForgeOutputCachingOptions
{
    public const string SectionName = "Forge:OutputCaching";

    public bool Enabled { get; init; } = false;

    public int DefaultExpirationSeconds { get; init; } = 60;
}
