using System.Text.Json;
using System.Text.RegularExpressions;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Publishing;

namespace LittlePublisher.Web.Services.Webmentions;

public interface IWebmentionContentService
{
    Task<object> CreateLikeAsync(CreateLikeRequest request, CancellationToken cancellationToken);
    Task<object> CreateReplyAsync(CreateReplyRequest request, CancellationToken cancellationToken);
    Task<object> CreateBlogrollAsync(CreateBlogrollRequest request, CancellationToken cancellationToken);
}

public sealed class WebmentionContentService : IWebmentionContentService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly AppConfiguration _config;
    private readonly IWebsiteRepository _repository;
    public WebmentionContentService(AppConfiguration config, IWebsiteRepository repository) { _config = config; _repository = repository; }

    public Task<object> CreateLikeAsync(CreateLikeRequest request, CancellationToken cancellationToken)
    {
        var target = ValidateTarget(request.TargetUrl);
        return CreateActivityAsync("like", target, request.Comment, null, cancellationToken);
    }

    public Task<object> CreateReplyAsync(CreateReplyRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Comment)) throw new InvalidOperationException("A reply comment is required.");
        var target = ValidateTarget(request.TargetUrl);
        return CreateActivityAsync("reply", target, request.Comment, request.TargetTitle, cancellationToken);
    }

    public async Task<object> CreateBlogrollAsync(CreateBlogrollRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new InvalidOperationException("A blog name is required.");
        var target = ValidateTarget(request.SiteUrl);
        if (!string.IsNullOrWhiteSpace(request.FeedUrl)) ValidateTarget(request.FeedUrl);
        var now = DateTimeOffset.UtcNow;
        var slug = Slug(request.Name);
        var activitySlug = $"liked-{slug}";
        var contentPath = $"blog/content/activity/{now:yyyy-MM}/{activitySlug}.md";
        var blogrollPath = $"blog/data/blogroll/{slug}.json";
        var markdown = ActivityMarkdown("like", target, request.Comment, request.Name, now);
        var blogroll = JsonSerializer.Serialize(new { schemaVersion = 1, name = request.Name.Trim(), siteUrl = target, feedUrl = request.FeedUrl, comment = request.Comment, activityUrl = ActivityUrl(now, activitySlug), addedUtc = now }, Json) + "\n";
        var result = await _repository.MutateFilesAsync([RepositoryFileMutation.Upsert(blogrollPath, blogroll), RepositoryFileMutation.Upsert(contentPath, markdown)], $"Add {request.Name.Trim()} to blogroll", cancellationToken);
        return new { url = ActivityUrl(now, activitySlug), filePath = contentPath, commitSha = result.CommitSha, status = "waiting-for-deployment" };
    }

    private async Task<object> CreateActivityAsync(string type, string target, string? comment, string? targetTitle, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var host = new Uri(target).IdnHost;
        var slug = Slug($"{type}-{targetTitle ?? host}-{now:HHmmssfff}");
        var path = $"blog/content/activity/{now:yyyy-MM}/{slug}.md";
        var markdown = ActivityMarkdown(type, target, comment, targetTitle, now);
        var result = await _repository.MutateFilesAsync([RepositoryFileMutation.Upsert(path, markdown)], $"Publish {type} activity for {host}", cancellationToken);
        return new { url = ActivityUrl(now, slug), filePath = path, commitSha = result.CommitSha, status = "waiting-for-deployment" };
    }

    private string ActivityUrl(DateTimeOffset date, string slug) => $"{_config.Website.Url.TrimEnd('/')}/activity/{date:yyyy/MM}/{slug}/";
    private static string ActivityMarkdown(string type, string target, string? comment, string? targetTitle, DateTimeOffset now)
    {
        var title = type == "reply" ? $"Reply to {targetTitle ?? new Uri(target).Host}" : $"Liked {targetTitle ?? new Uri(target).Host}";
        var relationship = type == "reply" ? "in_reply_to" : "like_of";
        return $"---\ntitle: {Yaml(title)}\ndate: {now:yyyy-MM-ddTHH:mm:sszzz}\nactivity_type: {type}\n{relationship}: {Yaml(target)}\n{(string.IsNullOrWhiteSpace(targetTitle) ? string.Empty : $"reply_to_title: {Yaml(targetTitle)}\n")}---\n\n{comment?.Trim()}\n";
    }
    private static string ValidateTarget(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) throw new InvalidOperationException("Target must be an absolute HTTP(S) URL without credentials.");
        return uri.AbsoluteUri;
    }
    private static string Slug(string value) => Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-') is { Length: > 0 } slug ? slug[..Math.Min(slug.Length, 80)] : "activity";
    private static string Yaml(string value) => $"'{value.Replace("'", "''", StringComparison.Ordinal)}'";
}
