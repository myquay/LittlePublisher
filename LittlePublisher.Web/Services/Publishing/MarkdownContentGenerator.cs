namespace LittlePublisher.Web.Services.Publishing;

public class MarkdownContentGenerator : IContentGenerator
{
    public string GenerateMarkdown(PublishCreateRequest request, string url)
    {
        return IsArticle(request)
            ? GeneratePostMarkdown(request)
            : GenerateNoteMarkdown(request);
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
