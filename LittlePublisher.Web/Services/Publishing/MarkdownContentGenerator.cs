namespace LittlePublisher.Web.Services.Publishing;

public class MarkdownContentGenerator : IContentGenerator
{
    public string GenerateMarkdown(PublishCreateRequest request, string url)
    {
        var postType = string.IsNullOrWhiteSpace(request.PostType)
            ? string.IsNullOrWhiteSpace(request.Name) ? "note" : "article"
            : request.PostType.ToLowerInvariant();
        return postType switch
        {
            "book-review" => GenerateBookMarkdown(request, url),
            "article" => GeneratePostMarkdown(request),
            "note" => GenerateNoteMarkdown(request),
            _ => GenerateActivityMarkdown(request)
        };
    }

    private static string GenerateBookMarkdown(PublishCreateRequest request, string url)
    {
        var properties = ContentTypeCatalog.NormalizeProperties("book-review", request.Properties);
        var error = ContentTypeCatalog.Validate("book-review", properties, true, request.Content);
        if (error is not null) throw new InvalidOperationException(error);
        string? Get(string key) => properties.TryGetValue(key, out var values) ? values.FirstOrDefault() : null;
        static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value,
            new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        var lines = new List<string>
        {
            "---", "type: book-review",
            $"title: {Quote(request.Name ?? Get("book-title")! + " — review")}",
            $"date: {request.PublishedUtc:yyyy-MM-ddTHH:mm:sszzz}",
            $"url: {Quote(new Uri(url).AbsolutePath)}",
            $"rating: {Get("rating")}", "book:"
        };
        foreach (var (property, field) in new[] { ("book-title", "title"), ("book-author", "author"),
            ("book-cover", "cover"), ("book-cover-alt", "cover_alt"), ("book-isbn", "isbn"), ("book-url", "url") })
            if (Get(property) is { } value) lines.Add($"  {field}: {Quote(value)}");
        if (Get("date-read") is { } date) lines.Add($"date_read: {Quote(date)}");
        if (!string.IsNullOrWhiteSpace(request.Summary)) lines.Add($"summary: {Quote(request.Summary)}");
        if (request.Categories.Count > 0)
        {
            lines.Add("tags:");
            lines.AddRange(request.Categories.Select(category => $"  - {Quote(category)}"));
        }
        lines.AddRange(["---", "", request.Content]);
        return string.Join(Environment.NewLine, lines);
    }

    private static string GeneratePostMarkdown(PublishCreateRequest request)
    {
        var lines = new List<string>
        {
            "---",
            $"publishDate: {request.PublishedUtc:yyyy-MM-ddTHH:mm:sszzz}",
            $"title: {YamlValue(request.Name!)}"
        };

        if (!string.IsNullOrWhiteSpace(request.Summary))
        {
            lines.Add($"summary: {YamlValue(request.Summary)}");
        }

        lines.Add($"url: /{request.Slug}");

        if (request.Categories.Count > 0)
        {
            lines.Add("tags:");
            lines.AddRange(request.Categories.Select(category => $"    - {YamlValue(category)}"));
        }

        lines.Add("---");
        lines.Add(string.Empty);
        lines.Add(request.Content);

        return string.Join(Environment.NewLine, lines);
    }

    private static string GenerateNoteMarkdown(PublishCreateRequest request)
    {
        var title = string.IsNullOrWhiteSpace(request.Name)
            ? BuildNoteTitle(request.Content)
            : request.Name;

        var lines = new List<string>
        {
            "---",
            $"date: {request.PublishedUtc:yyyy-MM-ddTHH:mm:sszzz}",
            $"title: {YamlValue(title)}",
            $"slug: /{request.Slug}",
            "---",
            string.Empty,
            request.Content
        };

        return string.Join(Environment.NewLine, lines);
    }

    private static bool IsArticle(PublishCreateRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.Name);
    }

    private static string GenerateActivityMarkdown(PublishCreateRequest request)
    {
        var properties = request.Properties ??
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var title = string.IsNullOrWhiteSpace(request.Name)
            ? BuildNoteTitle(request.Content)
            : request.Name;
        var lines = new List<string>
        {
            "---",
            $"title: {YamlValue(title!)}",
            $"date: {request.PublishedUtc:yyyy-MM-ddTHH:mm:sszzz}",
            $"activity_type: {YamlValue(request.PostType)}"
        };

        if (!string.IsNullOrWhiteSpace(request.Summary))
        {
            lines.Add($"summary: {YamlValue(request.Summary)}");
        }

        AddProperty(lines, properties, "photo", "photo");
        AddProperty(lines, properties, "alt", "alt");
        AddProperty(lines, properties, "location", "photo_location");
        AddProperty(lines, properties, "in-reply-to", "in_reply_to");
        AddProperty(lines, properties, "reply-to-title", "reply_to_title");
        AddProperty(lines, properties, "like-of", "like_of");
        AddProperty(lines, properties, "repost-of", "repost_of");
        AddProperty(lines, properties, "bookmark-of", "bookmark_of");
        AddProperty(
            lines,
            properties,
            "url",
            string.Equals(request.PostType, "blogroll", StringComparison.OrdinalIgnoreCase) ? "like_of" : "site_url");
        AddProperty(lines, properties, "feed", "feed_url");
        AddProperty(lines, properties, "start", "start");
        AddProperty(lines, properties, "end", "end");
        AddProperty(lines, properties, "audio", "audio");
        AddProperty(lines, properties, "video", "video");

        if (request.Categories.Count > 0)
        {
            lines.Add("tags:");
            lines.AddRange(request.Categories.Select(category => $"    - {YamlValue(category)}"));
        }

        lines.Add("---");
        lines.Add(string.Empty);
        lines.Add(request.Content);
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddProperty(
        List<string> lines,
        IReadOnlyDictionary<string, IReadOnlyList<string>> properties,
        string property,
        string frontMatter)
    {
        if (properties.TryGetValue(property, out var values) && values.FirstOrDefault() is { } value)
        {
            lines.Add($"{frontMatter}: {YamlValue(value)}");
        }
    }

    private static string BuildNoteTitle(string content)
    {
        var title = content
            .ReplaceLineEndings(" ")
            .Trim();

        if (title.Length > 80)
        {
            title = title[..80].TrimEnd();
        }

        return string.IsNullOrWhiteSpace(title) ? "Untitled note" : title;
    }

    private static string YamlValue(string value)
    {
        if (value.Any(character => character is ':' or '\'' or '"' or '[' or ']' or '{' or '}' or '#' or '\n' or '\r'))
        {
            return $"'{value.Replace("'", "''")}'";
        }

        return value;
    }
}
