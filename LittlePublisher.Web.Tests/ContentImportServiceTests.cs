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
        Assert.Equal("https://example.com/a-fine-little-post/", storage.SavedItem.PublishedUrl);
        Assert.Equal("A Fine Little Post", storage.SavedItem.Title);
        Assert.Equal(["indieweb", "micropub"], storage.SavedItem.Categories);
        Assert.Equal("blog/content/post/2026/a-fine-little-post.md", storage.SavedItem.RepositoryPath);
        Assert.Equal("abc123", storage.SavedItem.CommitSha);
        Assert.False(storage.SavedItem.Draft);
        Assert.Equal("Short summary", storage.SavedItem.Summary);
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
        Assert.Equal("https://example.com/note/2026-05/a-short-note/", storage.SavedItem.PublishedUrl);
        Assert.Equal("A short note from a Micropub client.", storage.SavedItem.Content);
    }

    [Fact]
    public async Task ImportRepositoryAsync_SkipsExistingItemsByDefault()
    {
        var storage = new CapturingStorage { Existing = true };
        var service = CreateService(storage, ArticleFile());

        var result = await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Skipped);
        Assert.Null(storage.SavedItem);
    }

    [Fact]
    public async Task ImportRepositoryAsync_OverwriteUpdatesExistingItems()
    {
        var storage = new CapturingStorage { Existing = true };
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
    public async Task ImportRepositoryAsync_SkipsHugoIndexFiles()
    {
        var storage = new CapturingStorage();
        var service = CreateService(
            storage,
            new WebsiteContentFile("blog/content/_index.md", "No published date", "abc123"),
            new WebsiteContentFile("blog/content/note/_index.md", "No published date", "abc123"),
            new WebsiteContentFile("blog/content/post/_index.md", "No published date", "abc123"),
            ArticleFile());

        var result = await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        Assert.Equal(4, result.Scanned);
        Assert.Equal(1, result.Imported);
        Assert.Equal(3, result.Skipped);
        Assert.Equal(0, result.Failed);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ImportRepositoryAsync_ImportsMissingPublishedDateAsDraft()
    {
        var storage = new CapturingStorage();
        var service = CreateService(
            storage,
            new WebsiteContentFile(
                RelativePath: "blog/content/about.md",
                Content: """
                    ---
                    title: About
                    draft: true
                    ---

                    About this site.
                    """,
                CommitSha: "abc123"));

        var result = await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        Assert.Equal(1, result.Imported);
        Assert.Equal(0, result.Failed);
        Assert.NotNull(storage.SavedItem);
        Assert.True(storage.SavedItem.Draft);
        Assert.Equal("https://example.com/about/", storage.SavedItem.PublishedUrl);
        Assert.Null(storage.SavedItem.PublishedUtc);
    }

    [Fact]
    public async Task ImportRepositoryAsync_ReportsMissingDateWithoutDraftFlagAsAmbiguous()
    {
        var storage = new CapturingStorage();
        var service = CreateService(
            storage,
            new WebsiteContentFile(
                RelativePath: "blog/content/about.md",
                Content: """
                    ---
                    title: About
                    ---

                    About this site.
                    """,
                CommitSha: "abc123"));

        var result = await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        Assert.Equal(1, result.Failed);
        Assert.Equal(1, result.Ambiguous);
        Assert.Null(storage.SavedItem);
        Assert.Contains("ambiguous", result.Errors[0].Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ImportRepositoryAsync_ReportsInvalidPublishedDate()
    {
        var storage = new CapturingStorage();
        var service = CreateService(
            storage,
            new WebsiteContentFile(
                RelativePath: "blog/content/note/2023-07/httpcompletionoption-responseheadersread.md",
                Content: """
                    ---
                    date: not-a-date
                    title: HttpCompletionOption ResponseHeadersRead
                    ---

                    Draft body.
                    """,
                CommitSha: "abc123"));

        var result = await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        Assert.Equal(0, result.Imported);
        Assert.Equal(1, result.Failed);
        Assert.Equal("Published date is invalid.", result.Errors[0].Message);
    }

    [Fact]
    public async Task ImportRepositoryAsync_PreservesAuthoringProperties()
    {
        var storage = new CapturingStorage();
        var service = CreateService(storage, ArticleFile());

        await service.ImportRepositoryAsync(new ImportRepositoryRequest(), CancellationToken.None);

        Assert.Equal("This is the body.", storage.SavedItem!.Content);
        Assert.Equal("https://example.com/a-fine-little-post/", storage.SavedItem.PublishedUrl);
        Assert.Equal("indieweb", storage.SavedItem.Categories[0]);
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

    private sealed class CapturingStorage : IPostStorage
    {
        public bool Existing { get; init; }

        public ImportedPost? SavedItem { get; private set; }

        public Task<(PostRecord Post, bool Created)> ImportPostAsync(ImportedPost post, bool overwrite, CancellationToken cancellationToken)
        {
            if (!Existing || overwrite) SavedItem = post;
            return Task.FromResult((Record(post), !Existing));
        }

        public Task<PostRecord?> GetPostByRepositoryPathAsync(string repositoryPath, CancellationToken cancellationToken) =>
            Task.FromResult(Existing ? Record(ArticleImport()) : null);
        public Task<PostRecord?> GetPostByUrlAsync(string url, CancellationToken cancellationToken) =>
            Task.FromResult(Existing ? Record(ArticleImport()) : null);
        public Task<PostRecord> CreatePostAsync(NewPost post, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> UpdatePostAsync(string postId, PostUpdate update, string expectedETag, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord?> GetPostAsync(string postId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<PostRecord>> GetRecentPostsAsync(int take, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> MarkPublishingAsync(string postId, int revision, string expectedETag, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> MarkPublishedAsync(string postId, int revision, string publishedUrl, string filePath, string commitSha, DateTimeOffset publishedUtc, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PostRecord> MarkPublishFailedAsync(string postId, int revision, string error, CancellationToken cancellationToken) => throw new NotSupportedException();

        private static PostRecord Record(ImportedPost post) => new(
            "post", post.Title, post.Content, post.Summary, post.Categories, post.Slug, post.PostType,
            post.Draft ? PostStates.Draft : PostStates.Published, 1, post.Draft ? null : 1,
            post.PublishedUtc, post.PublishedUtc, post.PublishedUrl, post.RepositoryPath, post.CommitSha,
            null, post.RepositoryPath, post.CommitSha, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "etag");

        private static ImportedPost ArticleImport() => new(
            "Existing", "Existing", null, [], "existing", "article", false, DateTimeOffset.UtcNow,
            "https://example.com/a-fine-little-post/", "blog/content/post/2026/a-fine-little-post.md", "commit");
    }
}
