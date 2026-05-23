using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Controllers;
using LittlePublisher.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Tests;

public class SetupControllerTests
{
    [Fact]
    public void PublicStatus_ReturnsOnlyAuthenticationChecksWithoutDisplayValues()
    {
        var controller = new SetupController(CreateConfiguredAppConfiguration());

        var result = Assert.IsType<OkObjectResult>(controller.PublicStatus());
        var status = Assert.IsType<SetupStatus>(result.Value);
        var checks = status.Groups.SelectMany(group => group.Checks).ToArray();
        var keys = checks.Select(check => check.Key).ToArray();

        Assert.True(status.Ready);
        Assert.Single(status.Groups);
        Assert.All(status.Groups, group => Assert.Equal("Authentication", group.Name));
        Assert.Contains("App:Website:Url", keys);
        Assert.Contains("App:IndieAuth:ClientId", keys);
        Assert.Contains("App:Jwt:Issuer", keys);
        Assert.Contains("App:Jwt:Audience", keys);
        Assert.Contains("App:Jwt:SecretKey", keys);
        Assert.DoesNotContain(keys, key => key.StartsWith("App:GitHub:", StringComparison.Ordinal));
        Assert.DoesNotContain(keys, key => key.StartsWith("App:Storage:", StringComparison.Ordinal));
        Assert.DoesNotContain(keys, key => key.StartsWith("App:ExternalToken:", StringComparison.Ordinal));
        Assert.All(checks, check => Assert.Null(check.DisplayValue));
    }

    [Fact]
    public void Status_RequiresAuthorizationWhilePublicStatusAllowsAnonymous()
    {
        var controllerAuthorize = Assert.Single(typeof(SetupController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        Assert.IsType<AuthorizeAttribute>(controllerAuthorize);

        var statusMethod = typeof(SetupController).GetMethod(nameof(SetupController.Status));
        var publicStatusMethod = typeof(SetupController).GetMethod(nameof(SetupController.PublicStatus));

        Assert.NotNull(statusMethod);
        Assert.NotNull(publicStatusMethod);
        Assert.Empty(statusMethod.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
        Assert.Single(publicStatusMethod.GetCustomAttributes(typeof(AllowAnonymousAttribute), inherit: true));
    }

    [Fact]
    public void PublicStatus_BlocksReadinessWhenJwtSecretIsDefaultPlaceholder()
    {
        var config = CreateConfiguredAppConfiguration();
        config.Jwt.SecretKey = "CHANGE_THIS_TO_A_SECURE_KEY_AT_LEAST_32_CHARACTERS";
        var controller = new SetupController(config);

        var result = Assert.IsType<OkObjectResult>(controller.PublicStatus());
        var status = Assert.IsType<SetupStatus>(result.Value);
        var jwtSecret = Assert.Single(
            status.Groups.SelectMany(group => group.Checks),
            check => check.Key == "App:Jwt:SecretKey");

        Assert.False(status.Ready);
        Assert.False(jwtSecret.Configured);
        Assert.True(jwtSecret.Warning);
        Assert.Null(jwtSecret.DisplayValue);
    }

    [Fact]
    public void JwtTokenService_RefusesToIssueTokenWithDefaultPlaceholderSecret()
    {
        var config = CreateConfiguredAppConfiguration();
        config.Jwt.SecretKey = "CHANGE_THIS_TO_A_SECURE_KEY_AT_LEAST_32_CHARACTERS";
        var service = new JwtTokenService(config);

        var exception = Assert.Throws<InvalidOperationException>(() => service.GenerateToken("https://myblog.test/"));

        Assert.Contains("App:Jwt:SecretKey", exception.Message);
    }

    private static AppConfiguration CreateConfiguredAppConfiguration()
    {
        return new AppConfiguration
        {
            Host = "https://publisher.myblog.test",
            Website = new WebsiteConfiguration
            {
                Url = "https://myblog.test",
                AuthorName = "Example Person",
                AuthorPhoto = "https://myblog.test/photo.jpg"
            },
            GitHub = new GitHubConfiguration
            {
                RepositoryUrl = "https://github.com/myblog/site.git",
                Branch = "main",
                Username = "myblog",
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
                ClientId = "https://publisher.myblog.test"
            },
            Jwt = new JwtConfiguration
            {
                Issuer = "https://publisher.myblog.test",
                Audience = "https://publisher.myblog.test",
                SecretKey = "a-secure-jwt-secret-key-with-enough-length"
            }
        };
    }
}
