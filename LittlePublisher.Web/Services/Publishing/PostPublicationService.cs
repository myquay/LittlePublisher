using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Storage;

namespace LittlePublisher.Web.Services.Publishing;

public class PostPublicationService : IPostPublicationService
{
    private readonly IPostStorage _storage;
    private readonly IPublishingService _publishing;
    private readonly AppConfiguration _config;

    public PostPublicationService(IPostStorage storage, IPublishingService publishing, AppConfiguration config)
    {
        _storage = storage;
        _publishing = publishing;
        _config = config;
    }

    public async Task<PostRecord> PublishAsync(string postId, CancellationToken cancellationToken)
    {
        var post = await _storage.GetPostAsync(postId, cancellationToken) ??
            throw new InvalidOperationException($"Post '{postId}' was not found.");
        var validation = ContentTypeCatalog.Validate(post.PostType, post.MicropubProperties, requireComplete: true);
        if (validation is not null)
        {
            throw new InvalidOperationException(validation);
        }
        var revision = post.WorkingRevision;
        var publishedUtc = post.PublishedUtc ??
            post.RequestedPublishedUtc ??
            DateTimeOffset.UtcNow;
        var projectedPath = BuildProjectedPath(post, publishedUtc);
        var pathOwner = await _storage.GetPostByRepositoryPathAsync(projectedPath, cancellationToken);
        if (pathOwner is not null && !string.Equals(pathOwner.Id, post.Id, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The GitHub path '{projectedPath}' is already owned by another post.");
        }

        await _storage.MarkPublishingAsync(post.Id, revision, post.ETag, cancellationToken);

        try
        {
            var result = await _publishing.PublishCreateAsync(
                new PublishCreateRequest(
                    Name: post.Title,
                    Content: post.Content,
                    Summary: post.Summary,
                    Categories: post.Categories,
                    PublishedUtc: publishedUtc,
                    Slug: post.Slug,
                    PostType: post.PostType,
                    Properties: post.MicropubProperties),
                cancellationToken);

            return await _storage.MarkPublishedAsync(
                post.Id,
                revision,
                result.Url,
                result.FilePath,
                result.CommitSha,
                publishedUtc,
                cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Azure.RequestFailedException)
        {
            await _storage.MarkPublishFailedAsync(post.Id, revision, ex.Message, cancellationToken);
            throw;
        }
    }

    public async Task<PostRecord> DeleteAsync(string postId, CancellationToken cancellationToken)
    {
        var post = await _storage.GetPostAsync(postId, cancellationToken) ??
            throw new InvalidOperationException($"Post '{postId}' was not found.");
        if (!string.IsNullOrWhiteSpace(post.FilePath))
        {
            await _publishing.DeleteAsync(post.FilePath, $"Delete {post.Title ?? post.Slug}", cancellationToken);
        }
        if (string.Equals(post.PostType, "blogroll", StringComparison.OrdinalIgnoreCase))
        {
            await _publishing.DeleteAsync(
                $"blog/data/blogroll/{post.Slug}.json",
                $"Remove {post.Title ?? post.Slug} from blogroll",
                cancellationToken);
        }

        return await _storage.SetDeletedAsync(post.Id, true, cancellationToken);
    }

    public async Task<PostRecord> UndeleteAsync(string postId, CancellationToken cancellationToken)
    {
        var post = await _storage.SetDeletedAsync(postId, false, cancellationToken);
        return post.PublishedRevision is null
            ? post
            : await PublishAsync(post.Id, cancellationToken);
    }

    private string BuildProjectedPath(PostRecord post, DateTimeOffset publishedUtc)
    {
        var contentPath = _config.GitHub.ContentPath.Trim('/');
        return post.PostType.ToLowerInvariant() switch
        {
            "article" => $"{contentPath}/post/{publishedUtc:yyyy}/{post.Slug}.md",
            "note" => $"{contentPath}/note/{publishedUtc:yyyy-MM}/{post.Slug}.md",
            _ => $"{contentPath}/activity/{publishedUtc:yyyy-MM}/{post.Slug}.md"
        };
    }
}
