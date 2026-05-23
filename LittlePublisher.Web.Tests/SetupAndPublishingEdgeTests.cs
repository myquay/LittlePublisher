using System.Reflection;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Controllers;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Tests;

public class SetupAndPublishingEdgeTests
{
    [Fact]
    public void Status_WhenExternalJwtIsEnabled_RequiresJwtSettingsAndSecrets()
    {
        var config = CreateConfiguredAppConfiguration();
        config.ExternalToken = new ExternalTokenConfiguration
        {
            Enabled = true,
            Mode = ExternalTokenConfiguration.JwtMode,
            Issuer = "",
            Audience = "api",
            SecretKey = "secret"
        };
        var controller = new SetupController(config);

        var status = GetStatus(controller.Status());
        var checks = status.Groups.SelectMany(group => group.Checks).ToArray();

        Assert.False(status.Ready);
        Assert.True(Check(checks, "App:ExternalToken:Issuer").Required);
        Assert.False(Check(checks, "App:ExternalToken:Issuer").Configured);
        Assert.True(Check(checks, "App:ExternalToken:SecretKey").Required);
        Assert.Equal("Configured", Check(checks, "App:ExternalToken:SecretKey").DisplayValue);
        Assert.False(Check(checks, "App:ExternalToken:IntrospectionEndpoint").Required);
    }

    [Fact]
    public void Status_WhenExternalModeIsUnsupported_MarksModeMissing()
    {
        var config = CreateConfiguredAppConfiguration();
        config.ExternalToken = new ExternalTokenConfiguration
        {
            Enabled = true,
            Mode = "Opaque"
        };
        var controller = new SetupController(config);

        var status = GetStatus(controller.Status());
        var mode = status.Groups.SelectMany(group => group.Checks).Single(check => check.Key == "App:ExternalToken:Mode");

        Assert.False(status.Ready);
        Assert.True(mode.Required);
        Assert.False(mode.Configured);
        Assert.Null(mode.DisplayValue);
    }

    [Fact]
    public void MarkdownContentGenerator_QuotesYamlAndTruncatesLongNoteTitle()
    {
        var generator = new MarkdownContentGenerator();
        var content = "This title has: yaml # characters and a whole lot of extra words that should be trimmed before becoming a note title.";
        var markdown = generator.GenerateMarkdown(
            new PublishCreateRequest(
                Name: null,
                Content: content,
                Summary: null,
                Categories: [],
                PublishedUtc: new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero),
                Slug: "note"),
            "https://example.com/note/");

        var titleLine = markdown.Split(Environment.NewLine).Single(line => line.StartsWith("title: ", StringComparison.Ordinal));

        Assert.Contains("title: 'This title has: yaml # characters", titleLine);
        Assert.DoesNotContain("becoming a note title", titleLine);
    }

    [Fact]
    public async Task PublishingService_NormalizesEmptySlugAndTruncatesLongCommitMessage()
    {
        var repository = new CapturingWebsiteRepository();
        var service = new PublishingService(CreateConfiguredAppConfiguration(), new MarkdownContentGenerator(), repository);

        var result = await service.PublishCreateAsync(
            new PublishCreateRequest(
                Name: null,
                Content: new string('x', 80),
                Summary: null,
                Categories: [],
                PublishedUtc: new DateTimeOffset(2026, 5, 22, 8, 0, 0, TimeSpan.Zero),
                Slug: "!!!"),
            CancellationToken.None);

        Assert.Equal("https://example.com/note/2026-05/post/", result.Url);
        Assert.Equal("blog/content/note/2026-05/post.md", result.FilePath);
        Assert.Equal(58, repository.CommitMessage.Length);
    }

    [Fact]
    public async Task TableStoragePublisherStorage_WithoutConnectionStringThrowsHelpfulError()
    {
        var storage = new TableStoragePublisherStorage(new AppConfiguration());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.CheckHealthAsync(CancellationToken.None));

        Assert.Contains("App:Storage:ConnectionString", exception.Message);
    }

    [Theory]
    [InlineData("LP", "LittlePublisher")]
    [InlineData("Little-Publisher_123", "LittlePublisher123")]
    public void TableStoragePublisherStorage_NormalizesTablePrefix(string input, string expected)
    {
        var method = typeof(TableStoragePublisherStorage).GetMethod("NormalizeTablePrefix", BindingFlags.NonPublic | BindingFlags.Static)!;

        Assert.Equal(expected, method.Invoke(null, [input]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("../post.md")]
    [InlineData("/tmp/post.md")]
    public void GitCliWebsiteRepository_RejectsUnsafeContentPaths(string relativePath)
    {
        var method = typeof(GitCliWebsiteRepository).GetMethod("ValidateRelativePath", BindingFlags.NonPublic | BindingFlags.Static)!;

        var exception = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, [relativePath]));

        Assert.IsType<InvalidOperationException>(exception.InnerException);
    }

    [Fact]
    public void GitCliWebsiteRepository_BuildsAuthenticatedHttpsRemoteAndRedactsErrors()
    {
        var repository = new GitCliWebsiteRepository(new AppConfiguration
        {
            GitHub = new GitHubConfiguration
            {
                RepositoryUrl = "https://github.com/example/site.git",
                Username = "",
                Token = "super-secret-token",
                Branch = "main"
            }
        });
        var remoteMethod = typeof(GitCliWebsiteRepository).GetMethod("BuildAuthenticatedRemoteUrl", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var redactMethod = typeof(GitCliWebsiteRepository).GetMethod("Redact", BindingFlags.NonPublic | BindingFlags.Static)!;

        var remote = Assert.IsType<string>(remoteMethod.Invoke(repository, []));
        var redacted = Assert.IsType<string>(redactMethod.Invoke(null, [$"fatal: {remote} failed"]));

        Assert.StartsWith("https://x-access-token:super-secret-token@github.com/", remote);
        Assert.DoesNotContain("super-secret-token", redacted);
        Assert.Contains("https://***@github.com", redacted);
    }

    private static SetupStatus GetStatus(IActionResult result)
    {
        return Assert.IsType<SetupStatus>(Assert.IsType<OkObjectResult>(result).Value);
    }

    private static SetupCheck Check(IEnumerable<SetupCheck> checks, string key)
    {
        return checks.Single(check => check.Key == key);
    }

    private static AppConfiguration CreateConfiguredAppConfiguration()
    {
        return new AppConfiguration
        {
            Host = "https://publisher.example",
            Website = new WebsiteConfiguration
            {
                Url = "https://example.com",
                AuthorName = "Example Person"
            },
            GitHub = new GitHubConfiguration
            {
                RepositoryUrl = "https://github.com/example/site.git",
                Branch = "main",
                Username = "example",
                Token = "github-token",
                ContentPath = "blog/content"
            },
            Storage = new StorageConfiguration
            {
                ConnectionString = "UseDevelopmentStorage=true",
                TablePrefix = "LittlePublisher"
            },
            IndieAuth = new IndieAuthConfiguration
            {
                ClientId = "https://publisher.example"
            },
            Jwt = new JwtConfiguration
            {
                Issuer = "https://publisher.example",
                Audience = "https://publisher.example",
                SecretKey = "a-secure-jwt-secret-key-with-enough-length"
            }
        };
    }

    private sealed class CapturingWebsiteRepository : IWebsiteRepository
    {
        public string CommitMessage { get; private set; } = string.Empty;

        public Task<string> PublishFileAsync(string relativePath, string content, string commitMessage, CancellationToken cancellationToken)
        {
            CommitMessage = commitMessage;

            return Task.FromResult("commit");
        }

        public Task CheckConnectionAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
