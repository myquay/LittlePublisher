using LittlePublisher.Web.Controllers;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Tests;

public class AdminControllerTests
{
    [Fact]
    public async Task Dashboard_ReturnsRecentJobsAndItems()
    {
        var storage = new StubStorage();
        var controller = new AdminController(storage, storage, new StubWebsiteRepository(), new StubContentImportService());

        var result = Assert.IsType<OkObjectResult>(await controller.Dashboard(CancellationToken.None));
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);

        Assert.Contains("job-1", json);
        Assert.Contains("https://example.com/post/", json);
    }

    [Fact]
    public async Task CheckStorage_WhenStorageFails_ReturnsServiceUnavailable()
    {
        var storage = new StubStorage { HealthException = new InvalidOperationException("storage offline") };
        var controller = new AdminController(
            storage,
            storage,
            new StubWebsiteRepository(),
            new StubContentImportService());

        var result = Assert.IsType<ObjectResult>(await controller.CheckStorage(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("storage offline", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task CheckGitHub_WhenRepositoryIsReachable_ReturnsOk()
    {
        var storage = new StubStorage();
        var controller = new AdminController(storage, storage, new StubWebsiteRepository(), new StubContentImportService());

        var result = Assert.IsType<OkObjectResult>(await controller.CheckGitHub(CancellationToken.None));

        Assert.Contains("reachable", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task CheckGitHub_WhenRepositoryFails_ReturnsServiceUnavailable()
    {
        var storage = new StubStorage();
        var controller = new AdminController(
            storage,
            storage,
            new StubWebsiteRepository { Exception = new IOException("git failed") },
            new StubContentImportService());

        var result = Assert.IsType<ObjectResult>(await controller.CheckGitHub(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("git failed", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task CheckGitHub_WhenGitCannotBeStarted_ReturnsServiceUnavailable()
    {
        var storage = new StubStorage();
        var controller = new AdminController(
            storage,
            storage,
            new StubWebsiteRepository { Exception = new System.ComponentModel.Win32Exception("git executable was not found") },
            new StubContentImportService());

        var result = Assert.IsType<ObjectResult>(await controller.CheckGitHub(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("git executable", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task ImportRepository_ReturnsImportSummary()
    {
        var storage = new StubStorage();
        var controller = new AdminController(storage, storage, new StubWebsiteRepository(), new StubContentImportService());

        var result = Assert.IsType<OkObjectResult>(await controller.ImportRepository(new ImportRepositoryRequest(), CancellationToken.None));
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);

        Assert.Contains("\"scanned\":1", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"imported\":1", json, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubWebsiteRepository : IWebsiteRepository
    {
        public Exception? Exception { get; init; }

        public Task<string> PublishFileAsync(string relativePath, string content, string commitMessage, CancellationToken cancellationToken)
        {
            return Task.FromResult("commit");
        }

        public Task<IReadOnlyList<WebsiteContentFile>> GetContentFilesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<WebsiteContentFile>>([]);
        }

        public Task CheckConnectionAsync(CancellationToken cancellationToken)
        {
            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class StubContentImportService : IContentImportService
    {
        public Task<ImportRepositoryResult> ImportRepositoryAsync(ImportRepositoryRequest request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new ImportRepositoryResult(
                Scanned: 1,
                Imported: 1,
                Skipped: 0,
                Failed: 0,
                Errors: []));
        }
    }

    private sealed class StubStorage : IPublisherStorage, IPostStorage
    {
        public Exception? HealthException { get; init; }

        public Task<PublishJobRecord> CreatePublishJobAsync(NewPublishJob job, CancellationToken cancellationToken)
        {
            return Task.FromResult(Job());
        }

        public Task<PublishJobRecord> CompletePublishJobAsync(string jobId, string publishedUrl, CancellationToken cancellationToken)
        {
            return Task.FromResult(Job());
        }

        public Task<PublishJobRecord> FailPublishJobAsync(string jobId, string error, CancellationToken cancellationToken)
        {
            return Task.FromResult(Job());
        }

        public Task<IReadOnlyList<PublishJobRecord>> GetRecentPublishJobsAsync(int take, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PublishJobRecord>>([Job()]);
        }

        public Task CheckHealthAsync(CancellationToken cancellationToken)
        {
            if (HealthException is not null)
            {
                throw HealthException;
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PostRecord>> GetRecentPostsAsync(int take, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PostRecord>>([Post()]);
        }

        public Task<PostRecord> CreatePostAsync(NewPost post, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> UpdatePostAsync(string postId, PostUpdate update, string expectedETag, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord?> GetPostAsync(string postId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord?> GetPostByUrlAsync(string url, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord?> GetPostByRepositoryPathAsync(string repositoryPath, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> MarkPublishingAsync(string postId, int revision, string expectedETag, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> MarkPublishedAsync(string postId, int revision, string publishedUrl, string filePath, string commitSha, DateTimeOffset publishedUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> MarkPublishFailedAsync(string postId, int revision, string error, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<(PostRecord Post, bool Created)> ImportPostAsync(ImportedPost post, bool overwrite, CancellationToken cancellationToken) => throw new NotSupportedException();

        private static PublishJobRecord Job()
        {
            return new PublishJobRecord(
                Id: "job-1",
                UserMe: "https://example.com/",
                ClientId: null,
                Action: "create",
                Status: "succeeded",
                PublishedUrl: "https://example.com/post/",
                Error: null,
                RequestJson: "{}",
                CreatedUtc: DateTimeOffset.UtcNow,
                UpdatedUtc: DateTimeOffset.UtcNow);
        }

        private static PostRecord Post()
        {
            return new PostRecord(
                "post-1", "Post", "Body", null, [], "post", "article", PostStates.Published,
                1, 1, null, DateTimeOffset.UtcNow, "https://example.com/post/", "content/post.md", "abc123",
                null, null, null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "etag");
        }
    }
}
