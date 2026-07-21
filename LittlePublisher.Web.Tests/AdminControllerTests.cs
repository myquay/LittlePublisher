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
        var controller = new AdminController(new StubStorage(), new StubWebsiteRepository(), new StubContentImportService());

        var result = Assert.IsType<OkObjectResult>(await controller.Dashboard(CancellationToken.None));
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);

        Assert.Contains("job-1", json);
        Assert.Contains("https://example.com/post/", json);
    }

    [Fact]
    public async Task CheckStorage_WhenStorageFails_ReturnsServiceUnavailable()
    {
        var controller = new AdminController(
            new StubStorage { HealthException = new InvalidOperationException("storage offline") },
            new StubWebsiteRepository(),
            new StubContentImportService());

        var result = Assert.IsType<ObjectResult>(await controller.CheckStorage(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("storage offline", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task CheckGitHub_WhenRepositoryIsReachable_ReturnsOk()
    {
        var controller = new AdminController(new StubStorage(), new StubWebsiteRepository(), new StubContentImportService());

        var result = Assert.IsType<OkObjectResult>(await controller.CheckGitHub(CancellationToken.None));

        Assert.Contains("reachable", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task CheckGitHub_WhenRepositoryFails_ReturnsServiceUnavailable()
    {
        var controller = new AdminController(
            new StubStorage(),
            new StubWebsiteRepository { Exception = new IOException("git failed") },
            new StubContentImportService());

        var result = Assert.IsType<ObjectResult>(await controller.CheckGitHub(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("git failed", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task CheckGitHub_WhenGitCannotBeStarted_ReturnsServiceUnavailable()
    {
        var controller = new AdminController(
            new StubStorage(),
            new StubWebsiteRepository { Exception = new System.ComponentModel.Win32Exception("git executable was not found") },
            new StubContentImportService());

        var result = Assert.IsType<ObjectResult>(await controller.CheckGitHub(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Contains("git executable", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public async Task ImportRepository_ReturnsImportSummary()
    {
        var controller = new AdminController(new StubStorage(), new StubWebsiteRepository(), new StubContentImportService());

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

    private sealed class StubStorage : IPublisherStorage
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

        public Task SavePublishedItemAsync(NewPublishedItem item, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<PublishedItemRecord?> GetPublishedItemByUrlAsync(string url, CancellationToken cancellationToken)
        {
            return Task.FromResult<PublishedItemRecord?>(Item());
        }

        public Task<IReadOnlyList<PublishJobRecord>> GetRecentPublishJobsAsync(int take, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PublishJobRecord>>([Job()]);
        }

        public Task<IReadOnlyList<PublishedItemRecord>> GetRecentPublishedItemsAsync(int take, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PublishedItemRecord>>([Item()]);
        }

        public Task CheckHealthAsync(CancellationToken cancellationToken)
        {
            if (HealthException is not null)
            {
                throw HealthException;
            }

            return Task.CompletedTask;
        }

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

        private static PublishedItemRecord Item()
        {
            return new PublishedItemRecord(
                Id: "item-1",
                Url: "https://example.com/post/",
                Title: "Post",
                Content: "Body",
                Categories: [],
                PublishedUtc: DateTimeOffset.UtcNow,
                FilePath: "content/post.md",
                CommitSha: "abc123",
                PropertiesJson: "{}");
        }
    }
}
