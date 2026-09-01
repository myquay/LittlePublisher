using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Services.Publishing;

public class MarkdownPublishedItemParser
{
    private readonly AppConfiguration _config;

    public MarkdownPublishedItemParser(AppConfiguration config)
    {
        _config = config;
    }

    public ParsedPublishedItem Parse(WebsiteContentFile file)
    {
        var normalized = file.Content.ReplaceLineEndings("\n");
        var lines = normalized.Split('\n');

        if (lines.Length < 3 || lines[0].Trim() != "---")
        {
            throw new InvalidOperationException("Markdown file does not start with YAML front matter.");
        }

        var closingIndex = Array.FindIndex(lines, 1, line => line.Trim() == "---");

        if (closingIndex < 0)
        {
            throw new InvalidOperationException("Markdown front matter is not closed.");
        }

        var frontMatter = ParseFrontMatter(lines[1..closingIndex]);
        var body = BuildBody(lines[(closingIndex + 1)..]);
        var published = ReadPublishedUtc(frontMatter);
        var explicitlyDraft = ReadBoolean(frontMatter, "draft");
        if (published is null && explicitlyDraft != true)
        {
            throw new InvalidOperationException("Publication status is ambiguous: add a published date or 'draft: true'.");
        }

        var title = ReadString(frontMatter, "title") ?? ReadString(frontMatter, "name");
        var summary = ReadString(frontMatter, "summary") ?? ReadString(frontMatter, "description");
        var categories = ReadStringList(frontMatter, "tags")
            .Concat(ReadStringList(frontMatter, "category"))
            .Concat(ReadStringList(frontMatter, "categories"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var url = BuildUrl(file.RelativePath, frontMatter);

        return new ParsedPublishedItem(
            Url: url,
            Title: title,
            Content: body,
            Summary: summary,
            Categories: categories,
            PublishedUtc: published,
            FilePath: file.RelativePath,
            CommitSha: file.CommitSha,
            Draft: explicitlyDraft == true);
    }

    private static Dictionary<string, FrontMatterValue> ParseFrontMatter(IReadOnlyList<string> lines)
    {
        var values = new Dictionary<string, FrontMatterValue>(StringComparer.OrdinalIgnoreCase);
        string? currentListKey = null;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trimmed = line.Trim();

            if (currentListKey is not null && trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                values[currentListKey].Items.Add(Unquote(trimmed[2..].Trim()));
                continue;
            }

            var separatorIndex = line.IndexOf(':', StringComparison.Ordinal);

            if (separatorIndex <= 0)
            {
                currentListKey = null;
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                values[key] = new FrontMatterValue(null, []);
                currentListKey = key;
            }
            else
            {
                values[key] = new FrontMatterValue(Unquote(value), []);
                currentListKey = null;
            }
        }

        return values;
    }

    private DateTimeOffset? ReadPublishedUtc(IReadOnlyDictionary<string, FrontMatterValue> frontMatter)
    {
        var published = ReadString(frontMatter, "publishDate") ??
            ReadString(frontMatter, "date") ??
            ReadString(frontMatter, "published");

        if (string.IsNullOrWhiteSpace(published))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(NormalizePublishedDate(published), out var publishedUtc))
        {
            throw new InvalidOperationException("Published date is invalid.");
        }

        return publishedUtc;
    }

    private static string NormalizePublishedDate(string value)
    {
        var timeSeparator = value.IndexOf('T');
        if (timeSeparator >= 0 &&
            timeSeparator + 2 < value.Length &&
            char.IsDigit(value[timeSeparator + 1]) &&
            value[timeSeparator + 2] == ':')
        {
            return value.Insert(timeSeparator + 1, "0");
        }

        return value;
    }

    private string BuildUrl(string relativePath, IReadOnlyDictionary<string, FrontMatterValue> frontMatter)
    {
        var explicitUrl = ReadString(frontMatter, "url");

        if (!string.IsNullOrWhiteSpace(explicitUrl))
        {
            return NormalizePublicUrl(explicitUrl);
        }

        var inferredPath = InferPublishedPath(relativePath);

        if (!string.IsNullOrWhiteSpace(inferredPath))
        {
            return NormalizePublicUrl(inferredPath);
        }

        var slug = ReadString(frontMatter, "slug");

        if (!string.IsNullOrWhiteSpace(slug))
        {
            return NormalizePublicUrl(slug);
        }

        throw new InvalidOperationException("Published URL could not be inferred.");
    }

    private string? InferPublishedPath(string relativePath)
    {
        var contentPath = _config.GitHub.ContentPath.Trim('/');
        var normalizedPath = relativePath.Replace('\\', '/');

        if (!normalizedPath.StartsWith($"{contentPath}/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var contentRelativePath = normalizedPath[(contentPath.Length + 1)..];
        var parts = contentRelativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 3 &&
            string.Equals(parts[0], "post", StringComparison.OrdinalIgnoreCase))
        {
            return Path.GetFileNameWithoutExtension(parts[2]);
        }

        if (parts.Length == 3 &&
            string.Equals(parts[0], "note", StringComparison.OrdinalIgnoreCase))
        {
            return $"note/{parts[1]}/{Path.GetFileNameWithoutExtension(parts[2])}";
        }

        return Path.ChangeExtension(contentRelativePath, null);
    }

    private string NormalizePublicUrl(string value)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return EnsureTrailingSlash(absoluteUri.ToString());
        }

        var baseUrl = _config.Website.Url.TrimEnd('/');
        var path = value.Trim('/');

        return EnsureTrailingSlash($"{baseUrl}/{path}");
    }

    private static string EnsureTrailingSlash(string value)
    {
        return value.EndsWith("/", StringComparison.Ordinal) ? value : $"{value}/";
    }

    private static string BuildBody(IReadOnlyList<string> lines)
    {
        if (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines = lines.Skip(1).ToArray();
        }

        return string.Join("\n", lines);
    }

    private static string? ReadString(IReadOnlyDictionary<string, FrontMatterValue> frontMatter, string key)
    {
        return frontMatter.TryGetValue(key, out var value) ? value.Value : null;
    }

    private static IReadOnlyList<string> ReadStringList(IReadOnlyDictionary<string, FrontMatterValue> frontMatter, string key)
    {
        if (!frontMatter.TryGetValue(key, out var value))
        {
            return Array.Empty<string>();
        }

        if (value.Items.Count > 0)
        {
            return value.Items;
        }

        return string.IsNullOrWhiteSpace(value.Value)
            ? Array.Empty<string>()
            : value.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static bool? ReadBoolean(IReadOnlyDictionary<string, FrontMatterValue> frontMatter, string key)
    {
        var value = ReadString(frontMatter, key);
        return bool.TryParse(value, out var result) ? result : null;
    }

    private static string Unquote(string value)
    {
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
        {
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal);
        }

        return value;
    }

    private sealed record FrontMatterValue(string? Value, List<string> Items);
}
