namespace LittlePublisher.Web.Configuration;

public static class CorsConfiguration
{
    public static string[] GetAllowedOrigins(AppConfiguration config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var allowedEditors = config.AllowedEditors ?? [];
        var invalidEditor = allowedEditors.FirstOrDefault(origin => !IsHttpOrigin(origin));
        if (invalidEditor is not null)
        {
            throw new InvalidOperationException(
                $"App:AllowedEditors contains an invalid origin: '{invalidEditor}'. " +
                "Each value must contain only an absolute HTTP or HTTPS origin.");
        }

        return new[] { config.Host }
            .Concat(allowedEditors)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsHttpOrigin(string origin)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        return string.Equals(
            origin,
            uri.GetComponents(UriComponents.SchemeAndServer, UriFormat.UriEscaped),
            StringComparison.OrdinalIgnoreCase);
    }
}
