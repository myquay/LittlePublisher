using System.Text.RegularExpressions;
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
        var normalizedRequest = request with { Slug = NormalizeSlug(request.Slug) };
        var url = BuildPublishedUrl(normalizedRequest);
        var filePath = BuildContentPath(normalizedRequest);
        var markdown = _contentGenerator.GenerateMarkdown(normalizedRequest, url);
        var commitMessage = BuildCommitMessage(normalizedRequest);
        var commitSha = await _websiteRepository.PublishFileAsync(filePath, markdown, commitMessage, cancellationToken);

        return new PublishCreateResult(url, filePath, commitSha);
    }

    public static string BuildSlug(string? name, string content)
    {
        return Slugify(name ?? content);
    }

    private string BuildPublishedUrl(PublishCreateRequest request)
    {
        var baseUrl = _config.Website.Url.TrimEnd('/');

        if (!IsArticle(request))
        {
            return $"{baseUrl}/note/{request.PublishedUtc:yyyy-MM}/{request.Slug}/";
        }

        return $"{baseUrl}/{request.Slug}/";
    }

    private string BuildContentPath(PublishCreateRequest request)
    {
        var contentPath = _config.GitHub.ContentPath.Trim('/');

        if (IsArticle(request))
        {
            return $"{contentPath}/post/{request.PublishedUtc:yyyy}/{request.Slug}.md";
        }

        return $"{contentPath}/note/{request.PublishedUtc:yyyy-MM}/{request.Slug}.md";
    }

    private static bool IsArticle(PublishCreateRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.Name);
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
}
