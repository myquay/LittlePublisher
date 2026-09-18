using System.Net;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Controllers;
using LittlePublisher.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Tests;

public class BookReviewTests
{
    public static Dictionary<string, IReadOnlyList<string>> Properties() => new()
    {
        ["book-title"] = ["Book: a \"story\""], ["book-author"] = ["Author One, Author Two"],
        ["book-cover"] = ["cover.jpg"], ["rating"] = ["4"],
        ["book-isbn"] = ["0123456789"], ["book-url"] = ["https://openlibrary.org/works/OL1W"],
        ["date-read"] = ["2026-09-17"]
    };
    private static AppConfiguration Config() => new()
    {
        GitHub = new() { ContentPath = "blog/content" }, Website = new() { Url = "https://example.com" }
    };

    [Fact]
    public void GeneratedReview_RoundTripsNestedYamlWithoutOverwritingReviewTitleOrUrl()
    {
        var request = new PublishCreateRequest("My review", "My own words.\n\n<!--more-->\n\nMore words.", "Summary", ["books"],
            DateTimeOffset.Parse("2026-09-18T12:00:00+12:00"), "my-book", "book-review", Properties());
        var markdown = new MarkdownContentGenerator().GenerateMarkdown(request, "https://example.com/books/my-book/");
        Assert.Contains("rating: 4", markdown);
        Assert.Contains("isbn: \"0123456789\"", markdown);
        var parsed = new MarkdownPublishedItemParser(Config()).Parse(new("blog/content/books/my-book/index.md", markdown, "sha"));
        Assert.Equal("book-review", parsed.PostType);
        Assert.Equal("My review", parsed.Title);
        Assert.Equal("https://example.com/books/my-book/", parsed.Url);
        Assert.Equal(request.Content, parsed.Content);
        foreach (var property in Properties()) Assert.Equal(property.Value, parsed.Properties![property.Key]);
        Assert.Equal(["books"], parsed.Categories);
    }

    [Fact]
    public void ImportedBundle_InfersReviewUrlAndPreservesLocalCover()
    {
        var markdown = "---\ntitle: My review\ndate: 2026-09-17\ntype: book-review\nrating: 4\nbook: {title: Book, author: Author, cover: cover.jpg, url: 'https://book.example/'}\n---\n\nReview";
        var parsed = new MarkdownPublishedItemParser(Config()).Parse(new("blog/content/books/test/index.md", markdown, null));
        Assert.Equal("My review", parsed.Title);
        Assert.Equal("https://example.com/books/test/", parsed.Url);
        Assert.Equal(["cover.jpg"], parsed.Properties!["book-cover"]);
    }

    [Theory]
    [InlineData("rating", "0")]
    [InlineData("rating", "6")]
    [InlineData("rating", "3.5")]
    [InlineData("date-read", "2026-02-30")]
    [InlineData("book-cover", "http://example.com/cover.jpg")]
    [InlineData("book-cover", "//example.com/cover.jpg")]
    [InlineData("book-cover", "../cover.jpg")]
    [InlineData("book-url", "javascript:alert(1)")]
    public void InvalidValues_AreRejectedEvenForDrafts(string key, string value)
    {
        var properties = Properties(); properties[key] = [value];
        Assert.NotNull(ContentTypeCatalog.Validate("book-review", properties, false));
    }

    [Fact]
    public void IncompleteDrafts_SaveButCannotPublish()
    {
        Assert.Null(ContentTypeCatalog.Validate("book-review", new Dictionary<string, IReadOnlyList<string>>(), false));
        Assert.NotNull(ContentTypeCatalog.Validate("book-review", Properties(), true, " "));
        var properties = Properties(); properties.Remove("book-cover");
        Assert.NotNull(ContentTypeCatalog.Validate("book-review", properties, true, "Review"));
    }

    [Fact]
    public async Task Publishing_UsesBooksSectionAndPreservesImportedPath()
    {
        var repository = new Repository();
        var service = new PublishingService(Config(), new MarkdownContentGenerator(), repository);
        var request = new PublishCreateRequest("Review", "Words", null, [], DateTimeOffset.UtcNow, "test", "book-review", Properties());
        var result = await service.PublishCreateAsync(request, default);
        Assert.Equal("blog/content/books/test/index.md", result.FilePath);
        Assert.Equal("https://example.com/books/test/", result.Url);
        result = await service.PublishCreateAsync(request with {
            ExistingBookPath = "blog/content/books/old-review.md", ExistingBookUrl = "https://example.com/original/"
        }, default);
        Assert.Equal("blog/content/books/old-review.md", result.FilePath);
        Assert.Equal("https://example.com/original/", result.Url);
        Assert.Contains("url: \"/original/\"", repository.Content);
    }

    [Fact]
    public async Task Search_ReturnsWorkMetadataWithoutGuessingAnEdition_AndCaches()
    {
        var factory = new Factory("""{"docs":[{"key":"/works/OL1W","title":"Book","author_name":["A","B"],"cover_i":123,"first_publish_year":1999,"isbn":["wrong-edition"]}]}""");
        using var service = new OpenLibraryService(factory);
        var book = Assert.Single(await service.SearchAsync("Book author", default));
        Assert.Null(book.Isbn);
        Assert.Equal("A, B", book.Author);
        Assert.Equal("https://covers.openlibrary.org/b/id/123-L.jpg?default=false", book.CoverUrl);
        Assert.Contains("q=Book%20author", factory.Uri!.OriginalString);
        await service.SearchAsync("Book author", default);
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public async Task Search_IsbnUsesEditionMetadata_AndNormalizesHyphens()
    {
        var factory = new Factory("""{"title":"Edition","authors":[{"key":"/authors/OL1A"}],"publish_date":"2001","covers":[123]}""")
        { AuthorJson = "{\"name\":\"Writer\"}", Redirect = "https://openlibrary.org/books/OL1M.json" };
        using var service = new OpenLibraryService(factory);
        var book = Assert.Single(await service.SearchAsync("0-12345678-9", default));
        Assert.Equal("0123456789", book.Isbn);
        Assert.Equal("Edition", book.Title);
        Assert.Equal("Writer", book.Author);
        Assert.StartsWith("https://covers.openlibrary.org/", book.CoverUrl);
        Assert.Contains(factory.Paths, p => p == "/isbn/0123456789.json");
    }

    [Fact]
    public async Task Search_FailureOffersManualEntry_AndInvalidQueriesDoNotCallProvider()
    {
        using var service = new OpenLibraryService(new Factory("", HttpStatusCode.TooManyRequests));
        var controller = new BooksController(service);
        Assert.IsType<BadRequestObjectResult>(await controller.Search("x", default));
        var response = Assert.IsType<ObjectResult>(await controller.Search("Book", default));
        Assert.Equal(503, response.StatusCode);
        Assert.Contains("manually", System.Text.Json.JsonSerializer.Serialize(response.Value));
    }


    [Fact]
    public async Task IsbnLookup_RejectsRedirectToAnotherHost()
    {
        var factory = new Factory("{}") { Redirect = "https://other.example/books/OL1M.json" };
        using var service = new OpenLibraryService(factory);
        await Assert.ThrowsAsync<HttpRequestException>(() => service.SearchAsync("0123456789", default));
        Assert.Equal(1, factory.Calls);
    }

    [Fact]
    public async Task IsbnLookup_MissingEditionReturnsNoResults()
    {
        using var service = new OpenLibraryService(new Factory("", HttpStatusCode.NotFound));
        Assert.Empty(await service.SearchAsync("0123456789", default));
    }

    private sealed class Factory(string json, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler, IHttpClientFactory
    {
        public int Calls; public Uri? Uri;
        public List<string> Paths = [];
        public string? AuthorJson;
        public string? Redirect;
        public HttpClient CreateClient(string name) => new(this, false) { BaseAddress = new("https://openlibrary.org/") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++; Uri = request.RequestUri; Paths.Add(Uri!.AbsolutePath);
            if (Redirect is not null && request.RequestUri!.AbsolutePath.StartsWith("/isbn/"))
            {
                var redirect = new HttpResponseMessage(HttpStatusCode.Found);
                redirect.Headers.Location = new Uri(Redirect);
                return Task.FromResult(redirect);
            }
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(request.RequestUri!.AbsolutePath.StartsWith("/authors/") ? AuthorJson ?? json : json) });
        }
    }
    private sealed class Repository : IWebsiteRepository
    {
        public Task CheckConnectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public string Content = "";
        public Task<string> PublishFileAsync(string relativePath, string content, string commitMessage, CancellationToken cancellationToken)
        { Content = content; return Task.FromResult("sha"); }
        public Task<IReadOnlyList<WebsiteContentFile>> GetContentFilesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
