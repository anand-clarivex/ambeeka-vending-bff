using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using Pervaxis.Vpa.Vending.Models.Configuration;

namespace Pervaxis.Vpa.Vending.Services;

public sealed class ForgeDataRedaction
{
    private static readonly Regex EmailPattern = new(
        @"(?<local>[^@\s]+)@(?<domain>[^@\s]+\.[^@\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    private static readonly Regex PhonePattern = new(
        @"\b(?:\+?\d{1,3}[-. ]?)?(?:\(?\d{3}\)?[-. ]?)?\d{3}[-. ]?\d{4}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IOptionsMonitor<ForgeDataClassificationOptions> _options;

    public ForgeDataRedaction(IOptionsMonitor<ForgeDataClassificationOptions> options)
    {
        _options = options;
    }

    public string Classification => _options.CurrentValue.DefaultClassification;

    public string Redact(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        value = EmailPattern.Replace(value, "***@***");
        value = PhonePattern.Replace(value, "[redacted-phone]");
        return value;
    }

    public bool IsSensitiveKey(string key)
    {
        var keys = _options.CurrentValue.SensitiveKeys;
        return keys.Any(candidate => string.Equals(candidate, key, StringComparison.OrdinalIgnoreCase));
    }
}
