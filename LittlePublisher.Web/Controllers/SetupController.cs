using LittlePublisher.Web.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

/// <summary>
/// Exposes first-run setup and configuration readiness status.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/[controller]")]
public class SetupController : ControllerBase
{
    private readonly AppConfiguration _config;

    public SetupController(AppConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Returns grouped setup checks without exposing secret values.
    /// </summary>
    /// <returns>Current setup readiness.</returns>
    [HttpGet("status")]
    public IActionResult Status()
    {
        var groups = new[]
        {
            new SetupGroup("Application", new[]
            {
                Required("App:Host", _config.Host, "Public URL for this LittlePublisher instance.")
            }),
            new SetupGroup("Website", new[]
            {
                Required("App:Website:Url", _config.Website.Url, "Website that Micropub posts will be published to."),
                Optional("App:Website:AuthorName", _config.Website.AuthorName, "Default author name for generated content."),
                Optional("App:Website:AuthorPhoto", _config.Website.AuthorPhoto, "Default author photo URL for generated content.")
            }),
            new SetupGroup("GitHub publishing", new[]
            {
                Required("App:GitHub:RepositoryUrl", _config.GitHub.RepositoryUrl, "Website repository checkout URL."),
                Required("App:GitHub:Branch", _config.GitHub.Branch, "Branch LittlePublisher should commit to."),
                Required("App:GitHub:Username", _config.GitHub.Username, "GitHub username for repository writes."),
                Secret("App:GitHub:Token", _config.GitHub.Token, "GitHub token with permission to push website changes."),
                Required("App:GitHub:ContentPath", _config.GitHub.ContentPath, "Repository-relative folder for generated posts.")
            }),
            new SetupGroup("Durable storage", new[]
            {
                Secret("App:Storage:ConnectionString", _config.Storage.ConnectionString, "Azure Table Storage connection string."),
                Required("App:Storage:TablePrefix", _config.Storage.TablePrefix, "Prefix for LittlePublisher storage tables.")
            }),
            new SetupGroup("Authentication", new[]
            {
                Required("App:IndieAuth:ClientId", _config.IndieAuth.ClientId, "Client ID used for IndieAuth login."),
                Required("App:Jwt:Issuer", _config.Jwt.Issuer, "JWT issuer."),
                Required("App:Jwt:Audience", _config.Jwt.Audience, "JWT audience."),
                Secret("App:Jwt:SecretKey", _config.Jwt.SecretKey, "JWT signing key with at least 32 characters.")
            })
        };

        var requiredChecks = groups.SelectMany(group => group.Checks).Where(check => check.Required);
        var missingCount = requiredChecks.Count(check => !check.Configured);
        var warningCount = groups.SelectMany(group => group.Checks).Count(check => check.Warning);

        return Ok(new SetupStatus(
            Ready: missingCount == 0,
            MissingRequiredCount: missingCount,
            WarningCount: warningCount,
            Groups: groups));
    }

    private static SetupCheck Required(string key, string? value, string description)
    {
        var configured = HasConfiguredValue(value);

        return new SetupCheck(
            Key: key,
            Description: description,
            Required: true,
            Secret: false,
            Configured: configured,
            Warning: configured && LooksLikePlaceholder(value),
            DisplayValue: configured ? value : null);
    }

    private static SetupCheck Optional(string key, string? value, string description)
    {
        var configured = HasConfiguredValue(value);

        return new SetupCheck(
            Key: key,
            Description: description,
            Required: false,
            Secret: false,
            Configured: configured,
            Warning: configured && LooksLikePlaceholder(value),
            DisplayValue: configured ? value : null);
    }

    private static SetupCheck Secret(string key, string? value, string description)
    {
        var configured = HasConfiguredValue(value);

        return new SetupCheck(
            Key: key,
            Description: description,
            Required: true,
            Secret: true,
            Configured: configured,
            Warning: configured && LooksLikePlaceholder(value),
            DisplayValue: configured ? "Configured" : null);
    }

    private static bool HasConfiguredValue(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) && !LooksLikePlaceholder(value);
    }

    private static bool LooksLikePlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToUpperInvariant();

        return normalized is "HOST" or "TODO" or "TBD" ||
            normalized.Contains("CHANGE_THIS") ||
            normalized.Contains("YOUR-") ||
            normalized.Contains("YOUR_") ||
            normalized.Contains("EXAMPLE.COM");
    }
}

public record SetupStatus(bool Ready, int MissingRequiredCount, int WarningCount, IEnumerable<SetupGroup> Groups);

public record SetupGroup(string Name, IEnumerable<SetupCheck> Checks);

public record SetupCheck(
    string Key,
    string Description,
    bool Required,
    bool Secret,
    bool Configured,
    bool Warning,
    string? DisplayValue);
