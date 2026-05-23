using System.Security.Claims;
using System.Text;
using System.Text.Json;
using AspNet.Security.IndieAuth.Infrastructure;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Controllers;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Tests;

public class MicropubControllerTests
{
    [Fact]
    public async Task Get_Config_ReturnsMicropubEndpointAndSupportedPostTypes()
    {
        var controller = CreateController();

        var result = Assert.IsType<OkObjectResult>(await controller.Get("config", null, CancellationToken.None));
        var json = JsonSerializer.Serialize(result.Value, JsonOptions);

        Assert.Contains("\"micropub\":\"https://publisher.example/micropub\"", json);
        Assert.Contains("\"type\":\"note\"", json);
        Assert.Contains("\"type\":\"article\"", json);
    }

    [Fact]
    public async Task Post_JsonEntry_PublishesAndPersistsItem()
    {
        var publishing = new CapturingPublishingService();
        var storage = new CapturingPublisherStorage();
        var controller = CreateController(publishing: publishing, storage: storage);
        SetUser(controller, me: "https://example.com/", scopes: "profile create", clientId: "client-app");
        SetJsonBody(controller, """
            {
              "type": ["h-entry"],
              "properties": {
                "name": ["A Good Day"],
                "content": [{ "value": "The post body." }],
                "summary": ["Short version"],
                "category": ["indieweb", "tests"],
                "published": ["2026-05-20T12:30:00+00:00"]
              }
            }
            """);

        var result = Assert.IsType<CreatedResult>(await controller.Post(CancellationToken.None));

        Assert.Equal("https://example.com/published/a-good-day/", result.Location);
        Assert.Equal("A Good Day", publishing.Request!.Name);
        Assert.Equal("The post body.", publishing.Request.Content);
        Assert.Equal("a-good-day", publishing.Request.Slug);
        Assert.Equal(new DateTimeOffset(2026, 5, 20, 12, 30, 0, TimeSpan.Zero), publishing.Request.PublishedUtc);
        Assert.Equal("succeeded", storage.CompletedStatus);
        Assert.Equal("https://example.com/", storage.CreatedJob!.UserMe);
        Assert.Equal("client-app", storage.CreatedJob.ClientId);
        Assert.NotNull(storage.SavedItem);
        Assert.Equal("A Good Day", storage.SavedItem!.Title);
        Assert.Contains("indieweb", storage.SavedItem.Categories);
        Assert.Contains("\"summary\"", storage.SavedItem.PropertiesJson);
    }

    [Fact]
    public async Task Post_FormEntry_PublishesNoteWithCategories()
    {
        var publishing = new CapturingPublishingService();
        var storage = new CapturingPublisherStorage();
        var controller = CreateController(publishing: publishing, storage: storage);
        SetUser(controller, me: "https://example.com", scopes: "create");
        SetFormBody(controller, new Dictionary<string, string[]>
        {
            ["h"] = ["entry"],
            ["content"] = ["A note from a form client."],
            ["category"] = ["note", "micropub"],
            ["published"] = ["2026-05-21T09:00:00+12:00"]
        });

        var result = Assert.IsType<CreatedResult>(await controller.Post(CancellationToken.None));

        Assert.Equal("https://example.com/published/a-note-from-a-form-client/", result.Location);
        Assert.Null(publishing.Request!.Name);
        Assert.Equal("A note from a form client.", publishing.Request.Content);
        Assert.Equal(["note", "micropub"], publishing.Request.Categories);
    }

    [Fact]
    public async Task Post_WithoutCreateScope_ReturnsInsufficientScope()
    {
        var controller = CreateController();
        SetUser(controller, me: "https://example.com/", scopes: "profile");

        var result = Assert.IsType<ObjectResult>(await controller.Post(CancellationToken.None));

        Assert.Equal(StatusCodes.Status403Forbidden, result.StatusCode);
        Assert.Equal("Bearer error=\"insufficient_scope\", scope=\"create\"", controller.Response.Headers.WWWAuthenticate);
    }

    [Fact]
    public async Task Post_ForDifferentMe_ReturnsForbid()
    {
        var controller = CreateController();
        SetUser(controller, me: "https://someone-else.example/", scopes: "create");

        Assert.IsType<ForbidResult>(await controller.Post(CancellationToken.None));
    }

    [Fact]
    public async Task Post_UnsupportedEntryType_ReturnsBadRequest()
    {
        var controller = CreateController();
        SetUser(controller, me: "https://example.com/", scopes: "create");
        SetJsonBody(controller, """{"type":["h-card"],"properties":{"name":["Nope"]}}""");

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Post(CancellationToken.None));

        Assert.Contains("Only h=entry", JsonSerializer.Serialize(result.Value, JsonOptions));
    }

    [Fact]
    public async Task Post_PublishFailure_MarksJobFailedAndReturnsUnavailable()
    {
        var storage = new CapturingPublisherStorage();
        var controller = CreateController(
            publishing: new CapturingPublishingService { Exception = new IOException("git push failed") },
            storage: storage);
        SetUser(controller, me: "https://example.com/", scopes: "create");
        SetJsonBody(controller, """{"type":["h-entry"],"properties":{"content":["Body"]}}""");

        var result = Assert.IsType<ObjectResult>(await controller.Post(CancellationToken.None));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.Equal("failed", storage.CompletedStatus);
        Assert.Equal("git push failed", storage.FailureError);
    }

    [Fact]
    public async Task Get_SourceWithoutExternalToken_ReturnsChallenge()
    {
        var controller = CreateController();

        Assert.IsType<ChallengeResult>(await controller.Get("source", "https://example.com/published/post/", CancellationToken.None));
    }

    [Fact]
    public async Task Get_UnsupportedQuery_ReturnsBadRequest()
    {
        var controller = CreateController();

        var result = Assert.IsType<BadRequestObjectResult>(await controller.Get("anything", null, CancellationToken.None));

        Assert.Contains("Unsupported Micropub query", JsonSerializer.Serialize(result.Value, JsonOptions));
    }

    private static MicropubController CreateController(
        AppConfiguration? config = null,
        IPublishingService? publishing = null,
        IPublisherStorage? storage = null)
    {
        var controller = new MicropubController(
            config ?? CreateConfig(),
            publishing ?? new CapturingPublishingService(),
            storage ?? new CapturingPublisherStorage());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        return controller;
    }

    private static AppConfiguration CreateConfig()
    {
        return new AppConfiguration
        {
            Host = "https://publisher.example/",
            Website = new WebsiteConfiguration { Url = "https://example.com/" },
            ExternalToken = new ExternalTokenConfiguration { Enabled = false }
        };
    }

    private static void SetUser(MicropubController controller, string me, string scopes, string? clientId = null)
    {
        var claims = new List<Claim>
        {
            new(IndieAuthClaims.ME, me),
            new("scope", scopes)
        };

        if (clientId is not null)
        {
            claims.Add(new Claim("client_id", clientId));
        }

        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticationType: "Test"));
    }

    private static void SetJsonBody(ControllerBase controller, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        controller.Request.ContentType = "application/json";
        controller.Request.Body = new MemoryStream(bytes);
    }

    private static void SetFormBody(ControllerBase controller, Dictionary<string, string[]> values)
    {
        var encoded = string.Join("&", values.SelectMany(pair => pair.Value.Select(value =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(value)}")));
        var bytes = Encoding.UTF8.GetBytes(encoded);
        controller.Request.ContentType = "application/x-www-form-urlencoded";
        controller.Request.Body = new MemoryStream(bytes);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private sealed class CapturingPublishingService : IPublishingService
    {
        public PublishCreateRequest? Request { get; private set; }

        public Exception? Exception { get; init; }

        public Task<PublishCreateResult> PublishCreateAsync(PublishCreateRequest request, CancellationToken cancellationToken)
        {
            Request = request;

            if (Exception is not null)
            {
                throw Exception;
            }

            return Task.FromResult(new PublishCreateResult(
                $"https://example.com/published/{request.Slug}/",
                $"content/{request.Slug}.md",
                "abc123"));
        }
    }

    private sealed class CapturingPublisherStorage : IPublisherStorage
    {
        public NewPublishJob? CreatedJob { get; private set; }

        public NewPublishedItem? SavedItem { get; private set; }

        public string? CompletedStatus { get; private set; }

        public string? FailureError { get; private set; }

        public Task<PublishJobRecord> CreatePublishJobAsync(NewPublishJob job, CancellationToken cancellationToken)
        {
            CreatedJob = job;

            return Task.FromResult(Job("running"));
        }

        public Task<PublishJobRecord> CompletePublishJobAsync(string jobId, string publishedUrl, CancellationToken cancellationToken)
        {
            CompletedStatus = "succeeded";

            return Task.FromResult(Job("succeeded", publishedUrl: publishedUrl));
        }

        public Task<PublishJobRecord> FailPublishJobAsync(string jobId, string error, CancellationToken cancellationToken)
        {
            CompletedStatus = "failed";
            FailureError = error;

            return Task.FromResult(Job("failed", error: error));
        }

        public Task SavePublishedItemAsync(NewPublishedItem item, CancellationToken cancellationToken)
        {
            SavedItem = item;

            return Task.CompletedTask;
        }

        public Task<PublishedItemRecord?> GetPublishedItemByUrlAsync(string url, CancellationToken cancellationToken)
        {
            return Task.FromResult<PublishedItemRecord?>(null);
        }

        public Task<IReadOnlyList<PublishJobRecord>> GetRecentPublishJobsAsync(int take, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PublishJobRecord>>([]);
        }

        public Task<IReadOnlyList<PublishedItemRecord>> GetRecentPublishedItemsAsync(int take, CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<PublishedItemRecord>>([]);
        }

        public Task CheckHealthAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private static PublishJobRecord Job(string status, string? publishedUrl = null, string? error = null)
        {
            return new PublishJobRecord(
                Id: "job-1",
                UserMe: "https://example.com/",
                ClientId: "client-app",
                Action: "create",
                Status: status,
                PublishedUrl: publishedUrl,
                Error: error,
                RequestJson: "{}",
                CreatedUtc: DateTimeOffset.UtcNow,
                UpdatedUtc: DateTimeOffset.UtcNow);
        }
    }
}
