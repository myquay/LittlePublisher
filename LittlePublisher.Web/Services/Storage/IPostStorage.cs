namespace LittlePublisher.Web.Services.Storage;

public interface IPostStorage
{
    Task<PostRecord> CreatePostAsync(NewPost post, CancellationToken cancellationToken);

    Task<PostRecord> UpdatePostAsync(string postId, PostUpdate update, string expectedETag, CancellationToken cancellationToken);

    Task<PostRecord?> GetPostAsync(string postId, CancellationToken cancellationToken);

    Task<PostRecord?> GetPostByUrlAsync(string url, CancellationToken cancellationToken);

    Task<PostRecord?> GetPostByRepositoryPathAsync(string repositoryPath, CancellationToken cancellationToken);

    Task<IReadOnlyList<PostRecord>> GetRecentPostsAsync(int take, CancellationToken cancellationToken);

    Task<PostRecord> MarkPublishingAsync(string postId, int revision, string expectedETag, CancellationToken cancellationToken);

    Task<PostRecord> MarkPublishedAsync(
        string postId,
        int revision,
        string publishedUrl,
        string filePath,
        string commitSha,
        DateTimeOffset publishedUtc,
        CancellationToken cancellationToken);

    Task<PostRecord> MarkPublishFailedAsync(string postId, int revision, string error, CancellationToken cancellationToken);

    Task<PostRecord> SetDeletedAsync(string postId, bool deleted, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This post storage implementation does not support delete and undelete.");

    Task<(PostRecord Post, bool Created)> ImportPostAsync(ImportedPost post, bool overwrite, CancellationToken cancellationToken);
}
