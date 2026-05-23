using System.Text.Json;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services.Storage;

namespace LittlePublisher.Web.Tests;

public class ContentImportServiceTests
{
    [Fact]
    public async Task ImportRepositoryAsync_ImportsArticleWithExplicitUrl()
    {
        var storage = new CapturingStorage();
        var service = CreateService(
            storage,
            new WebsiteContentFile(
                RelativePath: "blog/content/post/2026/a-fine-little-post.md",
                Content: """
                    ---
                    publishDate: 2026-05-18T10:30:00+12:00
                    title: A Fine Little Post
                    summary: Short summary
                    url: /a-fine-little-post
                    tags:
                        - indieweb
                        - micropub
                    ---

                    This is the body.
                    """,
                CommitSha: "abc123"));

        var result = await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        Assert.Equal(1, result.Scanned);
        Assert.Equal(1, result.Imported);
        Assert.Empty(result.Errors);
        Assert.NotNull(storage.SavedItem);
        Assert.Equal("https://example.com/a-fine-little-post/", storage.SavedItem.Url);
        Assert.Equal("A Fine Little Post", storage.SavedItem.Title);
        Assert.Equal(["indieweb", "micropub"], storage.SavedItem.Categories);
        Assert.Equal("blog/content/post/2026/a-fine-little-post.md", storage.SavedItem.FilePath);
        Assert.Equal("abc123", storage.SavedItem.CommitSha);
        Assert.Contains("\"summary\":[\"Short summary\"]", storage.SavedItem.PropertiesJson);
    }

    [Fact]
    public async Task ImportRepositoryAsync_InfersGeneratedNoteUrlFromPathBeforeSlug()
    {
        var storage = new CapturingStorage();
        var service = CreateService(
            storage,
            new WebsiteContentFile(
                RelativePath: "blog/content/note/2026-05/a-short-note.md",
                Content: """
                    ---
                    date: 2026-05-18T09:15:00+00:00
                    title: A short note
                    slug: /a-short-note
                    ---

                    A short note from a Micropub client.
                    """,
                CommitSha: "abc123"));

        await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        Assert.NotNull(storage.SavedItem);
        Assert.Equal("https://example.com/note/2026-05/a-short-note/", storage.SavedItem.Url);
        Assert.Equal("A short note from a Micropub client.", storage.SavedItem.Content);
    }

    [Fact]
    public async Task ImportRepositoryAsync_SkipsExistingItemsByDefault()
    {
        var storage = new CapturingStorage
        {
            ExistingItem = new PublishedItemRecord(
                Id: "existing",
                Url: "https://example.com/a-fine-little-post/",
                Title: "Existing",
                Content: "Existing",
                Categories: [],
                PublishedUtc: DateTimeOffset.UtcNow,
                FilePath: null,
                CommitSha: null,
                PropertiesJson: "{}")
        };
        var service = CreateService(storage, ArticleFile());

        var result = await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Null(storage.SavedItem);
    }

    [Fact]
    public async Task ImportRepositoryAsync_OverwriteUpdatesExistingItems()
    {
        var storage = new CapturingStorage
        {
            ExistingItem = new PublishedItemRecord(
                Id: "existing",
                Url: "https://example.com/a-fine-little-post/",
                Title: "Existing",
                Content: "Existing",
                Categories: [],
                PublishedUtc: DateTimeOffset.UtcNow,
                FilePath: null,
                CommitSha: null,
                PropertiesJson: "{}")
        };
        var service = CreateService(storage, ArticleFile());

        var result = await service.ImportRepositoryAsync(new ImportRepositoryRequest(Overwrite: true), CancellationToken.None);

        Assert.Equal(1, result.Imported);
        Assert.NotNull(storage.SavedItem);
    }

    [Fact]
    public async Task ImportRepositoryAsync_DryRunDoesNotSaveItems()
    {
        var storage = new CapturingStorage();
        var service = CreateService(storage, ArticleFile());

        var result = await service.ImportRepositoryAsync(new ImportRepositoryRequest(DryRun: true), CancellationToken.None);

        Assert.Equal(1, result.Imported);
        Assert.Null(storage.SavedItem);
    }

    [Fact]
    public async Task ImportRepositoryAsync_ReportsMalformedFilesWithoutStoppingImport()
    {
        var storage = new CapturingStorage();
        var service = CreateService(
            storage,
            new WebsiteContentFile("blog/content/post/bad.md", "No front matter", "abc123"),
            ArticleFile());

        var result = await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        Assert.Equal(2, result.Scanned);
        Assert.Equal(1, result.Imported);
        Assert.Equal(1, result.Failed);
        Assert.Equal("blog/content/post/bad.md", result.Errors[0].FilePath);
    }

    [Fact]
    public async Task MarkdownPublishedItemParser_BuildsMicropubCompatibleProperties()
    {
        var storage = new CapturingStorage();
        var service = CreateService(storage, ArticleFile());

        await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        using var document = JsonDocument.Parse(storage.SavedItem!.PropertiesJson);
        var properties = document.RootElement.GetProperty("properties");

        Assert.Equal("h-entry", document.RootElement.GetProperty("type")[0].GetString());
        Assert.Equal("This is the body.", properties.GetProperty("content")[0].GetString());
        Assert.Equal("https://example.com/a-fine-little-post/", properties.GetProperty("url")[0].GetString());
        Assert.Equal("indieweb", properties.GetProperty("category")[0].GetString());
    }

    private static ContentImportService CreateService(CapturingStorage storage, params WebsiteContentFile[] files)
    {
        var config = new AppConfiguration
        {
            Website = new WebsiteConfiguration
            {
                Url = "https://example.com"
            },
            GitHub = new GitHubConfiguration
            {
                ContentPath = "blog/content"
            }
        };

        return new ContentImportService(
            new StubWebsiteRepository(files),
            storage,
            new MarkdownPublishedItemParser(config));
    }

    private static WebsiteContentFile ArticleFile()
    {
        return new WebsiteContentFile(
            RelativePath: "blog/content/post/2026/a-fine-little-post.md",
            Content: """
                ---
                publishDate: 2026-05-18T10:30:00+12:00
                title: A Fine Little Post
                url: /a-fine-little-post
                tags:
                    - indieweb
                ---

                This is the body.
                """,
            CommitSha: "abc123");
    }

    private sealed class StubWebsiteRepository : IWebsiteRepository
    {
        private readonly IReadOnlyList<WebsiteContentFile> _files;

        public StubWebsiteRepository(IReadOnlyList<WebsiteContentFile> files)
        {
            _files = files;
        }

        public Task<string> PublishFileAsync(string relativePath, string content, string commitMessage, CancellationToken cancellationToken)
        {
            return Task.FromResult("commit");
        }

        public Task<IReadOnlyList<WebsiteContentFile>> GetContentFilesAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_files);
        }

        public Task CheckConnectionAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class CapturingStorage : IPublisherStorage
    {
        public PublishedItemRecord? ExistingItem { get; init; }

        public NewPublishedItem? SavedItem { get; private set; }

        public Task<PublishJobRecord> CreatePublishJobAsync(NewPublishJob job, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<PublishJobRecord> CompletePublishJobAsync(string jobId, string publishedUrl, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<PublishJobRecord> FailPublishJobAsync(string jobId, string error, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SavePublishedItemAsync(NewPublishedItem item, CancellationToken cancellationToken)
        {
            SavedItem = item;

            return Task.CompletedTask;
        }

        public Task<PublishedItemRecord?> GetPublishedItemByUrlAsync(string url, CancellationToken cancellationToken)
        {
            return Task.FromResult(ExistingItem);
        }

        public Task<IReadOnlyList<PublishJobRecord>> GetRecentPublishJobsAsync(int take, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<PublishedItemRecord>> GetRecentPublishedItemsAsync(int take, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task CheckHealthAsync(CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
