using LittlePublisher.Web.Controllers;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Tests;

public class PostsControllerTests
{
    [Fact]
    public async Task Create_RoundTripsNormalizedPhotoProperties()
    {
        var storage = new CapturingPostStorage();
        var controller = new PostsController(storage, new NoopPublisherStorage(), new NoopPublicationService());
        var request = new PostsController.SavePostRequest(
            Title: "Nagano snow",
            Content: "The best snow",
            Summary: "A winter day",
            Categories: ["travel"],
            Slug: "nagano-snow",
            PostType: "PHOTO",
            RequestedPublishedUtc: null,
            Properties: new Dictionary<string, IReadOnlyList<string>>
            {
                ["PHOTO"] = [" https://example.com/media/snow.jpg "],
                ["alt"] = [" Snowboarder in deep snow "]
            });

        var result = Assert.IsType<CreatedAtActionResult>(await controller.Create(request, CancellationToken.None));
        var post = Assert.IsType<PostRecord>(result.Value);

        Assert.Equal("photo", storage.Created!.PostType);
        Assert.Equal(["https://example.com/media/snow.jpg"], storage.Created.Properties!["photo"]);
        Assert.Equal(["Snowboarder in deep snow"], post.MicropubProperties["alt"]);
    }

    [Fact]
    public async Task Create_RejectsUnknownExplicitPostType()
    {
        var storage = new CapturingPostStorage();
        var controller = new PostsController(storage, new NoopPublisherStorage(), new NoopPublicationService());
        var request = new PostsController.SavePostRequest(
            "Title", "Body", null, [], null, "mystery", null);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Create(request, CancellationToken.None));

        Assert.Contains("Unsupported post type", System.Text.Json.JsonSerializer.Serialize(result.Value));
        Assert.Null(storage.Created);
    }

    [Fact]
    public async Task Publish_RejectsIncompletePhotoDraftBeforeCreatingJob()
    {
        var storage = new CapturingPostStorage
        {
            Existing = Record(
                "photo",
                new Dictionary<string, IReadOnlyList<string>> { ["photo"] = ["/media/snow.jpg"] })
        };
        var jobs = new NoopPublisherStorage();
        var publication = new NoopPublicationService();
        var controller = new PostsController(storage, jobs, publication);

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Publish("post-1", CancellationToken.None));

        Assert.Contains("alt", System.Text.Json.JsonSerializer.Serialize(result.Value));
        Assert.False(jobs.Created);
        Assert.False(publication.Called);
    }

    private static PostRecord Record(
        string postType,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? properties = null)
    {
        var now = DateTimeOffset.UtcNow;
        return new PostRecord(
            "post-1", "Title", "Body", null, [], "slug", postType, PostStates.Draft,
            1, null, null, null, null, null, null, null, null, null, now, now, "etag", properties);
    }

    private sealed class CapturingPostStorage : IPostStorage
    {
        public NewPost? Created { get; private set; }
        public PostRecord? Existing { get; init; }

        public Task<PostRecord> CreatePostAsync(NewPost post, CancellationToken cancellationToken)
        {
            Created = post;
            return Task.FromResult(Record(post.PostType, post.Properties) with
            {
                Title = post.Title,
                Content = post.Content,
                Summary = post.Summary,
                Categories = post.Categories,
                Slug = post.Slug
            });
        }

        public Task<PostRecord?> GetPostAsync(string postId, CancellationToken cancellationToken) => Task.FromResult(Existing);
        public Task<PostRecord> UpdatePostAsync(string postId, PostUpdate update, string expectedETag, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord?> GetPostByUrlAsync(string url, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord?> GetPostByRepositoryPathAsync(string repositoryPath, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PostRecord>> GetRecentPostsAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> MarkPublishingAsync(string postId, int revision, string expectedETag, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> MarkPublishedAsync(string postId, int revision, string publishedUrl, string filePath, string commitSha, DateTimeOffset publishedUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> MarkPublishFailedAsync(string postId, int revision, string error, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<(PostRecord Post, bool Created)> ImportPostAsync(ImportedPost post, bool overwrite, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class NoopPublisherStorage : IPublisherStorage
    {
        public bool Created { get; private set; }

        public Task<PublishJobRecord> CreatePublishJobAsync(NewPublishJob job, CancellationToken cancellationToken)
        {
            Created = true;
            return Task.FromResult(Job());
        }

        public Task<PublishJobRecord> CompletePublishJobAsync(string jobId, string publishedUrl, CancellationToken cancellationToken) => Task.FromResult(Job());
        public Task<PublishJobRecord> FailPublishJobAsync(string jobId, string error, CancellationToken cancellationToken) => Task.FromResult(Job());
        public Task<IReadOnlyList<PublishJobRecord>> GetRecentPublishJobsAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<PublishJobRecord>>([]);
        public Task CheckHealthAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private static PublishJobRecord Job() => new(
            "job", "https://example.com", null, "publish", "pending", null, null, "{}",
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private sealed class NoopPublicationService : IPostPublicationService
    {
        public bool Called { get; private set; }

        public Task<PostRecord> PublishAsync(string postId, CancellationToken cancellationToken)
        {
            Called = true;
            return Task.FromResult(Record("article"));
        }

        public Task<PostRecord> DeleteAsync(string postId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> UndeleteAsync(string postId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
