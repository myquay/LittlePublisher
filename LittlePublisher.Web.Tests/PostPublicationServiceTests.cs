using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services.Storage;

namespace LittlePublisher.Web.Tests;

public class PostPublicationServiceTests
{
    [Fact]
    public async Task PublishAsync_PublishesWorkingRevisionAndRecordsProjection()
    {
        var storage = new PublicationStorage();
        var publishing = new PublicationGateway();
        var service = new PostPublicationService(storage, publishing, Config());

        var result = await service.PublishAsync("post-1", CancellationToken.None);

        Assert.Equal("Body in Azure", publishing.Request!.Content);
        Assert.Equal(2, result.PublishedRevision);
        Assert.Equal("https://example.com/post/", result.PublishedUrl);
        Assert.Null(result.LastPublishError);
    }

    [Fact]
    public async Task PublishAsync_WhenGitFails_PreservesWorkingRevisionAndMarksFailure()
    {
        var storage = new PublicationStorage();
        var service = new PostPublicationService(storage, new PublicationGateway { Failure = new IOException("push failed") }, Config());

        await Assert.ThrowsAsync<IOException>(() => service.PublishAsync("post-1", CancellationToken.None));

        Assert.Equal(PostStates.PublishFailed, storage.Post.State);
        Assert.Equal(2, storage.Post.WorkingRevision);
        Assert.Equal(1, storage.Post.PublishedRevision);
        Assert.Equal("push failed", storage.Post.LastPublishError);
    }

    [Fact]
    public async Task PublishAsync_PreservesPhotoTitleAndProperties()
    {
        var storage = new PublicationStorage
        {
            Post = BuildPhotoPost()
        };
        var publishing = new PublicationGateway();
        var service = new PostPublicationService(storage, publishing, Config());

        await service.PublishAsync("photo-1", CancellationToken.None);

        Assert.Equal("Nagano snow", publishing.Request!.Name);
        Assert.Equal("photo", publishing.Request.PostType);
        Assert.Equal(["/media/snow.jpg"], publishing.Request.Properties!["photo"]);
    }

    private sealed class PublicationGateway : IPublishingService
    {
        public PublishCreateRequest? Request { get; private set; }
        public Exception? Failure { get; init; }

        public Task<PublishCreateResult> PublishCreateAsync(PublishCreateRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            if (Failure is not null) throw Failure;
            return Task.FromResult(new PublishCreateResult("https://example.com/post/", "content/post.md", "commit-2"));
        }
    }

    private static AppConfiguration Config() => new()
    {
        GitHub = new GitHubConfiguration { ContentPath = "content" }
    };

    private sealed class PublicationStorage : IPostStorage
    {
        public PostRecord Post { get; set; } = BuildPost();

        public Task<PostRecord?> GetPostAsync(string postId, CancellationToken cancellationToken) =>
            Task.FromResult<PostRecord?>(Post);

        public Task<PostRecord> MarkPublishingAsync(string postId, int revision, string expectedETag, CancellationToken cancellationToken)
        {
            Post = Post with { State = PostStates.Publishing, ETag = "publishing" };
            return Task.FromResult(Post);
        }

        public Task<PostRecord> MarkPublishedAsync(string postId, int revision, string publishedUrl, string filePath, string commitSha, DateTimeOffset publishedUtc, CancellationToken cancellationToken)
        {
            Post = Post with
            {
                State = PostStates.Published,
                PublishedRevision = revision,
                PublishedUrl = publishedUrl,
                FilePath = filePath,
                CommitSha = commitSha,
                PublishedUtc = publishedUtc,
                LastPublishError = null
            };
            return Task.FromResult(Post);
        }

        public Task<PostRecord> MarkPublishFailedAsync(string postId, int revision, string error, CancellationToken cancellationToken)
        {
            Post = Post with { State = PostStates.PublishFailed, LastPublishError = error };
            return Task.FromResult(Post);
        }

        public Task<PostRecord> CreatePostAsync(NewPost post, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> UpdatePostAsync(string postId, PostUpdate update, string expectedETag, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord?> GetPostByUrlAsync(string url, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord?> GetPostByRepositoryPathAsync(string repositoryPath, CancellationToken cancellationToken) => Task.FromResult<PostRecord?>(null);
        public Task<IReadOnlyList<PostRecord>> GetRecentPostsAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<(PostRecord Post, bool Created)> ImportPostAsync(ImportedPost post, bool overwrite, CancellationToken cancellationToken) => throw new NotSupportedException();

        private static PostRecord BuildPost()
        {
            var now = DateTimeOffset.UtcNow;
            return new PostRecord(
                "post-1", "Post", "Body in Azure", null, [], "post", "article", PostStates.Published,
                2, 1, null, now.AddDays(-1), "https://example.com/post/", "content/post.md", "commit-1",
                null, null, null, now.AddDays(-2), now, "etag");
        }
    }

    private static PostRecord BuildPhotoPost()
    {
        var now = DateTimeOffset.UtcNow;
        return new PostRecord(
            "photo-1", "Nagano snow", "The best snow", null, [], "nagano-snow", "photo", PostStates.Draft,
            1, null, null, null, null, null, null, null, null, null, now, now, "etag",
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["photo"] = ["/media/snow.jpg"],
                ["alt"] = ["A snowboarder in deep snow"]
            });
    }
}
