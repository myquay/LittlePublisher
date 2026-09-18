namespace LittlePublisher.Web.Services.Publishing;

public static class ContentTypeCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> AllowedProperties =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["note"] = PropertySet(),
            ["article"] = PropertySet(),
            ["photo"] = PropertySet("photo", "alt", "location"),
            ["activity"] = PropertySet(),
            ["thought"] = PropertySet(),
            ["reply"] = PropertySet("in-reply-to", "reply-to-title"),
            ["like"] = PropertySet("like-of"),
            ["repost"] = PropertySet("repost-of"),
            ["bookmark"] = PropertySet("bookmark-of"),
            ["blogroll"] = PropertySet("url", "feed"),
            ["event"] = PropertySet("start", "end", "location"),
            ["audio"] = PropertySet("audio", "alt"),
            ["video"] = PropertySet("video", "alt")
        };

    public static IReadOnlyList<string> SupportedTypes { get; } = AllowedProperties.Keys.ToArray();

    public static string DisplayName(string postType) => postType switch
    {
        "blogroll" => "Blogroll entry",
        _ => char.ToUpperInvariant(postType[0]) + postType[1..]
    };

    public static string Normalize(string? postType, string? title)
    {
        if (string.IsNullOrWhiteSpace(postType))
        {
            return string.IsNullOrWhiteSpace(title) ? "note" : "article";
        }

        var normalized = postType.Trim().ToLowerInvariant();
        if (!AllowedProperties.ContainsKey(normalized))
        {
            throw new InvalidOperationException($"Unsupported post type '{postType}'.");
        }

        return normalized;
    }

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> NormalizeProperties(
        string postType,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? properties)
    {
        if (!AllowedProperties.TryGetValue(postType, out var allowed))
        {
            throw new InvalidOperationException($"Unsupported post type '{postType}'.");
        }

        if (properties is null || properties.Count == 0)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, values) in properties)
        {
            var normalizedKey = key.Trim().ToLowerInvariant();
            if (!allowed.Contains(normalizedKey))
            {
                throw new InvalidOperationException($"Property '{key}' is not supported for post type '{postType}'.");
            }

            var normalizedValues = values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (normalizedValues.Length > 0)
            {
                result[normalizedKey] = normalizedValues;
            }
        }

        return result;
    }

    public static string? Validate(
        string postType,
        IReadOnlyDictionary<string, IReadOnlyList<string>> properties,
        bool requireComplete)
    {
        static string? First(IReadOnlyDictionary<string, IReadOnlyList<string>> source, string key) =>
            source.TryGetValue(key, out var values) ? values.FirstOrDefault() : null;

        static bool ValidWebUrl(string value) =>
            value.StartsWith("/", StringComparison.Ordinal) ||
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

        static bool ValidMediaUrl(string value) =>
            value.StartsWith("/", StringComparison.Ordinal) ||
            Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;

        var urlProperty = postType switch
        {
            "photo" => "photo",
            "reply" => "in-reply-to",
            "like" => "like-of",
            "repost" => "repost-of",
            "bookmark" => "bookmark-of",
            "blogroll" => "url",
            "audio" => "audio",
            "video" => "video",
            _ => null
        };

        if (urlProperty is not null && First(properties, urlProperty) is { } url &&
            (postType is "photo" or "audio" or "video" ? !ValidMediaUrl(url) : !ValidWebUrl(url)))
        {
            return postType is "photo" or "audio" or "video"
                ? $"Property '{urlProperty}' must be an HTTPS URL or a site-relative path."
                : $"Property '{urlProperty}' must be an HTTP(S) URL or a site-relative path.";
        }

        if (!requireComplete)
        {
            return null;
        }

        var required = postType switch
        {
            "photo" => new[] { "photo", "alt" },
            "reply" => new[] { "in-reply-to" },
            "like" => new[] { "like-of" },
            "repost" => new[] { "repost-of" },
            "bookmark" => new[] { "bookmark-of" },
            "blogroll" => new[] { "url" },
            "event" => new[] { "start" },
            "audio" => new[] { "audio" },
            "video" => new[] { "video" },
            _ => Array.Empty<string>()
        };

        var missing = required.FirstOrDefault(property => string.IsNullOrWhiteSpace(First(properties, property)));
        return missing is null ? null : $"A {postType} requires the '{missing}' property before it can be published.";
    }

    private static IReadOnlySet<string> PropertySet(params string[] properties) =>
        properties.ToHashSet(StringComparer.OrdinalIgnoreCase);
}
