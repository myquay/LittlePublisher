namespace LittlePublisher.Web.Configuration;

public static class ConfigurationSecurity
{
    private const int MinimumJwtSecretKeyLength = 32;

    private static readonly HashSet<string> WeakSecretValues = new(StringComparer.OrdinalIgnoreCase)
    {
        "CHANGE_THIS_TO_A_SECURE_KEY_AT_LEAST_32_CHARACTERS",
        "SECRET",
        "PASSWORD",
        "CHANGEME",
        "CHANGE_ME",
        "DEFAULT",
        "TEST",
        "DEVELOPMENT"
    };

    public static bool HasConfiguredValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && !LooksLikePlaceholder(value);
    }

    public static bool LooksLikePlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant();

        return normalized is "HOST" or "TODO" or "TBD" ||
            normalized.Contains("CHANGE_THIS") ||
            normalized.Contains("YOUR-") ||
            normalized.Contains("YOUR_") ||
            normalized.Contains("EXAMPLE.COM");
    }

    public static bool IsSecureJwtSecretKey(string? value)
    {
        if (!HasConfiguredValue(value))
        {
            return false;
        }

        var trimmed = value!.Trim();

        return trimmed.Length >= MinimumJwtSecretKeyLength &&
            !WeakSecretValues.Contains(trimmed) &&
            !IsSingleRepeatedCharacter(trimmed);
    }

    private static bool IsSingleRepeatedCharacter(string value)
    {
        return value.All(character => character == value[0]);
    }
}
