using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Publishing;

namespace LittlePublisher.Web.Tests;

public class PublishingServiceTests
{
    [Fact]
    public async Task PublishCreateAsync_WritesNamedEntryAsHugoBlogPost()
    {
        var repository = new CapturingWebsiteRepository();
        var service = CreateService(repository);

        var result = await service.PublishCreateAsync(
            new PublishCreateRequest(
                Name: "A Fine Little Post",
                Content: "This is the body.",
                Summary: "Short summary",
                Categories: ["indieweb", "micropub"],
                PublishedUtc: new DateTimeOffset(2026, 05, 18, 10, 30, 00, TimeSpan.FromHours(12)),
                Slug: "a-fine-little-post"),
            CancellationToken.None);

        Assert.Equal("blog/content/post/2026/a-fine-little-post.md", repository.RelativePath);
        Assert.Equal("https://example.com/a-fine-little-post/", result.Url);
        Assert.Contains("publishDate: 2026-05-18T10:30:00+12:00", repository.Content);
        Assert.Contains("title: A Fine Little Post", repository.Content);
        Assert.Contains("summary: Short summary", repository.Content);
        Assert.Contains("url: /a-fine-little-post", repository.Content);
        Assert.Contains("    - indieweb", repository.Content);
        Assert.Contains("    - micropub", repository.Content);
        Assert.EndsWith("This is the body.", repository.Content);
    }

    [Fact]
    public async Task PublishCreateAsync_WritesUnnamedEntryAsHugoNote()
    {
        var repository = new CapturingWebsiteRepository();
        var service = CreateService(repository);

        var result = await service.PublishCreateAsync(
            new PublishCreateRequest(
                Name: null,
                Content: "A short note from a Micropub client.",
                Summary: null,
                Categories: [],
                PublishedUtc: new DateTimeOffset(2026, 05, 18, 9, 15, 00, TimeSpan.Zero),
                Slug: "a-short-note"),
            CancellationToken.None);

        Assert.Equal("blog/content/note/2026-05/a-short-note.md", repository.RelativePath);
        Assert.Equal("https://example.com/note/2026-05/a-short-note/", result.Url);
        Assert.Contains("date: 2026-05-18T09:15:00+00:00", repository.Content);
        Assert.Contains("title: A short note from a Micropub client.", repository.Content);
        Assert.Contains("slug: /a-short-note", repository.Content);
        Assert.EndsWith("A short note from a Micropub client.", repository.Content);
    }

    private static PublishingService CreateService(IWebsiteRepository repository)
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

        return new PublishingService(config, new MarkdownContentGenerator(), repository);
    }

    private sealed class CapturingWebsiteRepository : IWebsiteRepository
    {
        public string RelativePath { get; private set; } = string.Empty;

        public string Content { get; private set; } = string.Empty;

        public Task<string> PublishFileAsync(string relativePath, string content, string commitMessage, CancellationToken cancellationToken)
        {
            RelativePath = relativePath;
            Content = content;

            return Task.FromResult("abc123");
        }

        public Task CheckConnectionAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
