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
            "article" => GeneratePostMarkdown(request),
            "note" => GenerateNoteMarkdown(request),
            _ => GenerateActivityMarkdown(request)
        };
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

        AddProperty(lines, properties, "photo", "photo");
        AddProperty(lines, properties, "alt", "alt");
        AddProperty(lines, properties, "location", "photo_location");
        AddProperty(lines, properties, "in-reply-to", "in_reply_to");
        AddProperty(lines, properties, "like-of", "like_of");
        AddProperty(lines, properties, "repost-of", "repost_of");
        AddProperty(lines, properties, "bookmark-of", "bookmark_of");
        AddProperty(
            lines,
            properties,
            "url",
            string.Equals(request.PostType, "blogroll", StringComparison.OrdinalIgnoreCase) ? "like_of" : "site_url");
        AddProperty(lines, properties, "feed", "feed_url");

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
