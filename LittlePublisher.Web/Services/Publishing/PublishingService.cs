using System.Text.RegularExpressions;
using System.Text.Json;
using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Services.Publishing;

public class PublishingService : IPublishingService
{
    private readonly AppConfiguration _config;
    private readonly IContentGenerator _contentGenerator;
    private readonly IWebsiteRepository _websiteRepository;

    public PublishingService(
        AppConfiguration config,
        IContentGenerator contentGenerator,
        IWebsiteRepository websiteRepository)
    {
        _config = config;
        _contentGenerator = contentGenerator;
        _websiteRepository = websiteRepository;
    }

    public async Task<PublishCreateResult> PublishCreateAsync(PublishCreateRequest request, CancellationToken cancellationToken)
    {
        var normalizedRequest = request with
        {
            Slug = NormalizeSlug(request.Slug),
            PostType = string.IsNullOrWhiteSpace(request.PostType)
                ? string.IsNullOrWhiteSpace(request.Name) ? "note" : "article"
                : request.PostType
        };
        var url = BuildPublishedUrl(normalizedRequest);
        var filePath = BuildContentPath(normalizedRequest);
        var markdown = _contentGenerator.GenerateMarkdown(normalizedRequest, url);
        var commitMessage = BuildCommitMessage(normalizedRequest);
        string commitSha;
        if (string.Equals(normalizedRequest.PostType, "blogroll", StringComparison.OrdinalIgnoreCase))
        {
            var properties = normalizedRequest.Properties ??
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            var siteUrl = ReadFirst(properties, "url") ?? ReadFirst(properties, "bookmark-of") ?? string.Empty;
            var feedUrl = ReadFirst(properties, "feed");
            var blogrollPath = $"blog/data/blogroll/{normalizedRequest.Slug}.json";
            var blogrollJson = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                name = normalizedRequest.Name ?? normalizedRequest.Slug,
                siteUrl,
                feedUrl,
                comment = normalizedRequest.Content,
                activityUrl = url,
                addedUtc = normalizedRequest.PublishedUtc
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }) + "\n";
            var mutation = await _websiteRepository.MutateFilesAsync(
                [
                    RepositoryFileMutation.Upsert(filePath, markdown),
                    RepositoryFileMutation.Upsert(blogrollPath, blogrollJson)
                ],
                commitMessage,
                cancellationToken);
            commitSha = mutation.CommitSha;
        }
        else
        {
            commitSha = await _websiteRepository.PublishFileAsync(filePath, markdown, commitMessage, cancellationToken);
        }

        return new PublishCreateResult(url, filePath, commitSha);
    }

    public async Task<string> DeleteAsync(
        string relativePath,
        string commitMessage,
        CancellationToken cancellationToken)
    {
        var result = await _websiteRepository.MutateFilesAsync(
            [RepositoryFileMutation.Delete(relativePath)],
            commitMessage,
            cancellationToken);
        return result.CommitSha;
    }

    public static string BuildSlug(string? name, string content)
    {
        return Slugify(name ?? content);
    }

    private string BuildPublishedUrl(PublishCreateRequest request)
    {
        var baseUrl = _config.Website.Url.TrimEnd('/');

        return request.PostType.ToLowerInvariant() switch
        {
            "article" => $"{baseUrl}/{request.Slug}/",
            "note" => $"{baseUrl}/note/{request.PublishedUtc:yyyy-MM}/{request.Slug}/",
            _ => $"{baseUrl}/activity/{request.PublishedUtc:yyyy/MM}/{request.Slug}/"
        };
    }

    private string BuildContentPath(PublishCreateRequest request)
    {
        var contentPath = _config.GitHub.ContentPath.Trim('/');

        return request.PostType.ToLowerInvariant() switch
        {
            "article" => $"{contentPath}/post/{request.PublishedUtc:yyyy}/{request.Slug}.md",
            "note" => $"{contentPath}/note/{request.PublishedUtc:yyyy-MM}/{request.Slug}.md",
            _ => $"{contentPath}/activity/{request.PublishedUtc:yyyy-MM}/{request.Slug}.md"
        };
    }

    private static bool IsArticle(PublishCreateRequest request)
    {
        return string.Equals(request.PostType, "article", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCommitMessage(PublishCreateRequest request)
    {
        var label = request.Name;

        if (string.IsNullOrWhiteSpace(label))
        {
            label = request.Content.Length > 50 ? request.Content[..50] : request.Content;
        }

        return $"Publish {label}";
    }

    private static string NormalizeSlug(string value)
    {
        return Slugify(value);
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

        if (slug.Length > 64)
        {
            slug = slug[..64].Trim('-');
        }

        return string.IsNullOrWhiteSpace(slug) ? "post" : slug;
    }

    private static string? ReadFirst(
        IReadOnlyDictionary<string, IReadOnlyList<string>> properties,
        string key)
    {
        return properties.TryGetValue(key, out var values) ? values.FirstOrDefault() : null;
    }
}
