using LittlePublisher.Web.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

/// <summary>
/// Exposes first-run setup and configuration readiness status.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class SetupController : ControllerBase
{
    private readonly AppConfiguration _config;

    public SetupController(AppConfiguration config)
    {
        _config = config;
    }

    /// <summary>
    /// Returns only the setup checks required to make authentication possible.
    /// </summary>
    /// <returns>Current public authentication readiness.</returns>
    [AllowAnonymous]
    [HttpGet("public-status")]
    public IActionResult PublicStatus()
    {
        return Ok(CreateStatus(BuildAuthenticationGroups(redactValues: true)));
    }

    /// <summary>
    /// Returns grouped setup checks without exposing secret values.
    /// </summary>
    /// <returns>Current authenticated setup readiness.</returns>
    [HttpGet("status")]
    public IActionResult Status()
    {
        return Ok(CreateStatus(BuildFullGroups(redactValues: false)));
    }

    private IEnumerable<SetupGroup> BuildFullGroups(bool redactValues)
    {
        return new[]
        {
            new SetupGroup("Application", new[]
            {
                Required("App:Host", _config.Host, "Public URL for this LittlePublisher instance.", redactValues)
            }),
            new SetupGroup("Website", new[]
            {
                Required("App:Website:Url", _config.Website.Url, "Website that Micropub posts will be published to.", redactValues),
                Optional("App:Website:AuthorName", _config.Website.AuthorName, "Default author name for generated content.", redactValues),
                Optional("App:Website:AuthorPhoto", _config.Website.AuthorPhoto, "Default author photo URL for generated content.", redactValues)
            }),
            new SetupGroup("GitHub publishing", new[]
            {
                Required("App:GitHub:RepositoryUrl", _config.GitHub.RepositoryUrl, "Website repository checkout URL.", redactValues),
                Required("App:GitHub:Branch", _config.GitHub.Branch, "Branch LittlePublisher should commit to.", redactValues),
                Required("App:GitHub:Username", _config.GitHub.Username, "GitHub username for repository writes.", redactValues),
                Secret("App:GitHub:Token", _config.GitHub.Token, "GitHub token with permission to push website changes.", redactValues),
                Required("App:GitHub:ContentPath", _config.GitHub.ContentPath, "Repository-relative Hugo content root, for example blog/content.", redactValues)
            }),
            new SetupGroup("Durable storage", new[]
            {
                Secret("App:Storage:ConnectionString", _config.Storage.ConnectionString, "Azure Table Storage connection string.", redactValues),
                Required("App:Storage:TablePrefix", _config.Storage.TablePrefix, "Prefix for LittlePublisher storage tables.", redactValues)
            }),
            BuildAuthenticationGroup(redactValues, includeWebsiteUrl: false, includeExternalTokens: true)
        };
    }

    private IEnumerable<SetupGroup> BuildAuthenticationGroups(bool redactValues)
    {
        return new[]
        {
            BuildAuthenticationGroup(redactValues, includeWebsiteUrl: true, includeExternalTokens: false)
        };
    }

    private SetupGroup BuildAuthenticationGroup(bool redactValues, bool includeWebsiteUrl, bool includeExternalTokens)
    {
        var checks = new List<SetupCheck>
        {
            Required("App:IndieAuth:ClientId", _config.IndieAuth.ClientId, "Client ID used for IndieAuth login.", redactValues),
            Required("App:Jwt:Issuer", _config.Jwt.Issuer, "JWT issuer.", redactValues),
            Required("App:Jwt:Audience", _config.Jwt.Audience, "JWT audience.", redactValues),
            JwtSecret("App:Jwt:SecretKey", _config.Jwt.SecretKey, "JWT signing key with at least 32 characters.", redactValues)
        };

        if (includeWebsiteUrl)
        {
            checks.Insert(0, Required("App:Website:Url", _config.Website.Url, "Website URL used as the IndieAuth identity.", redactValues));
        }

        if (includeExternalTokens)
        {
            checks.InsertRange(1, new[]
            {
                Required("App:ExternalToken:Enabled", _config.ExternalToken.Enabled ? "true" : null, "Enable access tokens from an external IndieAuth server for Micropub clients.", redactValues),
                ExternalTokenModeSetting(),
                ExternalJwtSetting("App:ExternalToken:Issuer", _config.ExternalToken.Issuer, "External JWT issuer.", redactValues),
                ExternalJwtSetting("App:ExternalToken:Audience", _config.ExternalToken.Audience, "External JWT audience.", redactValues),
                ExternalJwtSecret("App:ExternalToken:SecretKey", _config.ExternalToken.SecretKey, "External JWT signing key.", redactValues),
                ExternalIntrospectionSetting("App:ExternalToken:IntrospectionEndpoint", _config.ExternalToken.IntrospectionEndpoint, "External token introspection endpoint.", redactValues),
                ExternalIntrospectionSecret("App:ExternalToken:IntrospectionToken", _config.ExternalToken.IntrospectionToken, "Bearer token used to authenticate introspection requests.", redactValues)
            });
        }

        return new SetupGroup("Authentication", checks);
    }

    private static SetupStatus CreateStatus(IEnumerable<SetupGroup> groups)
    {
        var requiredChecks = groups.SelectMany(group => group.Checks).Where(check => check.Required);
        var missingCount = requiredChecks.Count(check => !check.Configured);
        var warningCount = groups.SelectMany(group => group.Checks).Count(check => check.Warning);

        return new SetupStatus(
            Ready: missingCount == 0,
            MissingRequiredCount: missingCount,
            WarningCount: warningCount,
            Groups: groups);
    }

    private static SetupCheck Required(string key, string? value, string description, bool redactValue)
    {
        var configured = ConfigurationSecurity.HasConfiguredValue(value);

        return new SetupCheck(
            Key: key,
            Description: description,
            Required: true,
            Secret: false,
            Configured: configured,
            Warning: false,
            DisplayValue: configured && !redactValue ? value : null);
    }

    private static SetupCheck Optional(string key, string? value, string description, bool redactValue)
    {
        var configured = ConfigurationSecurity.HasConfiguredValue(value);

        return new SetupCheck(
            Key: key,
            Description: description,
            Required: false,
            Secret: false,
            Configured: configured,
            Warning: false,
            DisplayValue: configured && !redactValue ? value : null);
    }

    private static SetupCheck Secret(string key, string? value, string description, bool redactValue)
    {
        var configured = ConfigurationSecurity.HasConfiguredValue(value);

        return new SetupCheck(
            Key: key,
            Description: description,
            Required: true,
            Secret: true,
            Configured: configured,
            Warning: false,
            DisplayValue: configured && !redactValue ? "Configured" : null);
    }

    private static SetupCheck JwtSecret(string key, string? value, string description, bool redactValue)
    {
        var configured = ConfigurationSecurity.IsSecureJwtSecretKey(value);
        var hasValue = !string.IsNullOrWhiteSpace(value);

        return new SetupCheck(
            Key: key,
            Description: description,
            Required: true,
            Secret: true,
            Configured: configured,
            Warning: hasValue && !configured,
            DisplayValue: configured && !redactValue ? "Configured" : null);
    }

    private SetupCheck ExternalJwtSetting(string key, string? value, string description, bool redactValue)
    {
        return _config.ExternalToken.Enabled && IsExternalJwtMode()
            ? Required(key, value, description, redactValue)
            : Optional(key, value, description, redactValue);
    }

    private SetupCheck ExternalJwtSecret(string key, string? value, string description, bool redactValue)
    {
        return _config.ExternalToken.Enabled && IsExternalJwtMode()
            ? Secret(key, value, description, redactValue)
            : Optional(key, value, description, redactValue);
    }

    private SetupCheck ExternalIntrospectionSetting(string key, string? value, string description, bool redactValue)
    {
        return _config.ExternalToken.Enabled && IsExternalIntrospectionMode()
            ? Required(key, value, description, redactValue)
            : Optional(key, value, description, redactValue);
    }

    private SetupCheck ExternalIntrospectionSecret(string key, string? value, string description, bool redactValue)
    {
        return _config.ExternalToken.Enabled && IsExternalIntrospectionMode()
            ? Secret(key, value, description, redactValue)
            : Optional(key, value, description, redactValue);
    }

    private bool IsExternalJwtMode()
    {
        return _config.ExternalToken.IsJwtMode;
    }

    private bool IsExternalIntrospectionMode()
    {
        return _config.ExternalToken.IsIntrospectionMode;
    }

    private SetupCheck ExternalTokenModeSetting()
    {
        if (!_config.ExternalToken.Enabled)
        {
            return Optional("App:ExternalToken:Mode", _config.ExternalToken.Mode, "External token validation mode: Jwt or Introspection.", redactValue: false);
        }

        return new SetupCheck(
            Key: "App:ExternalToken:Mode",
            Description: "External token validation mode: Jwt or Introspection.",
            Required: true,
            Secret: false,
            Configured: _config.ExternalToken.IsSupportedMode,
            Warning: false,
            DisplayValue: _config.ExternalToken.IsSupportedMode ? _config.ExternalToken.Mode : null);
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
