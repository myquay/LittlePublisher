using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using AspNet.Security.IndieAuth;
using AspNet.Security.IndieAuth.Infrastructure;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

[ApiController]
[Route("micropub")]
public class MicropubController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppConfiguration _config;
    private readonly IPublisherStorage _storage;

    public MicropubController(AppConfiguration config, IPublisherStorage storage)
    {
        _config = config;
        _storage = storage;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get([FromQuery] string? q, [FromQuery] string? url, CancellationToken cancellationToken)
    {
        return q switch
        {
            "config" => Ok(GetConfigResponse()),
            "source" => await GetSourceAsync(url, cancellationToken),
            _ => BadRequest(new MicropubError("invalid_request", "Unsupported Micropub query."))
        };
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        if (!IsConfiguredUser())
        {
            return Forbid();
        }

        if (!HasScope("create"))
        {
            Response.Headers.WWWAuthenticate = "Bearer error=\"insufficient_scope\", scope=\"create\"";
            return StatusCode(StatusCodes.Status403Forbidden, new MicropubError("insufficient_scope", "The create scope is required."));
        }

        MicropubEntry entry;

        try
        {
            entry = await ReadEntryAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new MicropubError("invalid_request", ex.Message));
        }

        if (!string.Equals(entry.Type, "entry", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new MicropubError("invalid_request", "Only h=entry create requests are supported."));
        }

        if (string.IsNullOrWhiteSpace(entry.Content) && string.IsNullOrWhiteSpace(entry.Name))
        {
            return BadRequest(new MicropubError("invalid_request", "A Micropub entry requires content or name."));
        }

        var publishedUtc = entry.Published ?? DateTimeOffset.UtcNow;
        var publishedUrl = BuildPublishedUrl(entry, publishedUtc);
        var propertiesJson = JsonSerializer.Serialize(ToSourceProperties(entry, publishedUrl, publishedUtc), JsonOptions);
        var requestJson = JsonSerializer.Serialize(entry, JsonOptions);

        PublishJobRecord? job = null;

        try
        {
            job = await _storage.CreatePublishJobAsync(
                new NewPublishJob(
                    UserMe: GetAuthenticatedMe()!,
                    ClientId: User.FindFirst("client_id")?.Value,
                    Action: "create",
                    RequestJson: requestJson),
                cancellationToken);

            await _storage.SavePublishedItemAsync(
                new NewPublishedItem(
                    Url: publishedUrl,
                    Title: entry.Name,
                    Content: entry.Content ?? entry.Name ?? string.Empty,
                    Categories: entry.Categories,
                    PublishedUtc: publishedUtc,
                    FilePath: BuildContentPath(entry, publishedUtc),
                    CommitSha: null,
                    PropertiesJson: propertiesJson),
                cancellationToken);

            await _storage.CompletePublishJobAsync(job.Id, publishedUrl, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Azure.RequestFailedException)
        {
            if (job is not null)
            {
                await _storage.FailPublishJobAsync(job.Id, ex.Message, cancellationToken);
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new MicropubError("temporarily_unavailable", ex.Message));
        }

        Response.Headers.Location = publishedUrl;

        return Created(publishedUrl, new { url = publishedUrl });
    }

    private async Task<IActionResult> GetSourceAsync(string? url, CancellationToken cancellationToken)
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return Challenge();
        }

        if (!IsConfiguredUser())
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            return BadRequest(new MicropubError("invalid_request", "The url query parameter is required."));
        }

        try
        {
            var item = await _storage.GetPublishedItemByUrlAsync(url, cancellationToken);

            if (item is null)
            {
                return NotFound(new MicropubError("not_found", "The requested source item was not found."));
            }

            return Content(item.PropertiesJson, "application/json");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Azure.RequestFailedException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new MicropubError("temporarily_unavailable", ex.Message));
        }
    }

    private object GetConfigResponse()
    {
        var host = _config.Host.TrimEnd('/');

        return new Dictionary<string, object>
        {
            ["micropub"] = $"{host}/micropub",
            ["syndicate-to"] = Array.Empty<object>(),
            ["post-types"] = new[]
            {
                new { type = "note", name = "Note" },
                new { type = "article", name = "Article" }
            }
        };
    }

    private async Task<MicropubEntry> ReadEntryAsync(CancellationToken cancellationToken)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);

            return new MicropubEntry(
                Type: StripHPrefix(form["h"].FirstOrDefault()),
                Name: form["name"].FirstOrDefault(),
                Content: form["content"].FirstOrDefault(),
                Summary: form["summary"].FirstOrDefault(),
                Categories: form["category"]
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .ToArray(),
                Published: ParsePublished(form["published"].FirstOrDefault()));
        }

        if (Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var properties = root.TryGetProperty("properties", out var props) ? props : default;

            return new MicropubEntry(
                Type: StripHPrefix(ReadFirstString(root, "type")),
                Name: ReadFirstPropertyString(properties, "name"),
                Content: ReadContentProperty(properties),
                Summary: ReadFirstPropertyString(properties, "summary"),
                Categories: ReadPropertyStrings(properties, "category"),
                Published: ParsePublished(ReadFirstPropertyString(properties, "published")));
        }

        throw new InvalidOperationException("Micropub requests must be form-encoded or JSON.");
    }

    private Dictionary<string, object> ToSourceProperties(MicropubEntry entry, string url, DateTimeOffset publishedUtc)
    {
        var properties = new Dictionary<string, object>
        {
            ["content"] = new[] { entry.Content ?? entry.Name ?? string.Empty },
            ["published"] = new[] { publishedUtc.ToString("O") },
            ["url"] = new[] { url }
        };

        if (!string.IsNullOrWhiteSpace(entry.Name))
        {
            properties["name"] = new[] { entry.Name };
        }

        if (!string.IsNullOrWhiteSpace(entry.Summary))
        {
            properties["summary"] = new[] { entry.Summary };
        }

        if (entry.Categories.Count > 0)
        {
            properties["category"] = entry.Categories;
        }

        return new Dictionary<string, object>
        {
            ["type"] = new[] { "h-entry" },
            ["properties"] = properties
        };
    }

    private string BuildPublishedUrl(MicropubEntry entry, DateTimeOffset publishedUtc)
    {
        var slugSource = entry.Name ?? entry.Content ?? "post";
        var slug = Slugify(slugSource);
        var baseUrl = _config.Website.Url.TrimEnd('/');

        return $"{baseUrl}/{publishedUtc:yyyy/MM/dd}/{slug}/";
    }

    private string BuildContentPath(MicropubEntry entry, DateTimeOffset publishedUtc)
    {
        var slugSource = entry.Name ?? entry.Content ?? "post";
        var slug = Slugify(slugSource);
        var contentPath = _config.GitHub.ContentPath.Trim('/');

        return $"{contentPath}/{publishedUtc:yyyy-MM-dd}-{slug}.md";
    }

    private bool IsConfiguredUser()
    {
        var authenticatedMe = GetAuthenticatedMe()?.Canonicalize();
        var configuredMe = _config.Website.Url.Canonicalize();

        return !string.IsNullOrWhiteSpace(authenticatedMe) &&
            string.Equals(authenticatedMe, configuredMe, StringComparison.OrdinalIgnoreCase);
    }

    private string? GetAuthenticatedMe()
    {
        return User.FindFirst(IndieAuthClaims.ME)?.Value ??
            User.FindFirst("me")?.Value ??
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private bool HasScope(string requiredScope)
    {
        return User.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(scope => string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ReadFirstString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var first = value.EnumerateArray().FirstOrDefault();

            return first.ValueKind == JsonValueKind.String ? first.GetString() : null;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static string? ReadFirstPropertyString(JsonElement properties, string propertyName)
    {
        return ReadPropertyStrings(properties, propertyName).FirstOrDefault();
    }

    private static string? ReadContentProperty(JsonElement properties)
    {
        if (properties.ValueKind != JsonValueKind.Object ||
            !properties.TryGetProperty("content", out var content) ||
            content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var first = content.EnumerateArray().FirstOrDefault();

        if (first.ValueKind == JsonValueKind.String)
        {
            return first.GetString();
        }

        if (first.ValueKind == JsonValueKind.Object)
        {
            if (first.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.String)
            {
                return value.GetString();
            }

            if (first.TryGetProperty("html", out var html) && html.ValueKind == JsonValueKind.String)
            {
                return html.GetString();
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ReadPropertyStrings(JsonElement properties, string propertyName)
    {
        if (properties.ValueKind != JsonValueKind.Object ||
            !properties.TryGetProperty(propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!)
            .ToArray();
    }

    private static DateTimeOffset? ParsePublished(string? published)
    {
        return DateTimeOffset.TryParse(published, out var result) ? result : null;
    }

    private static string StripHPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.StartsWith("h-", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');

        if (slug.Length > 64)
        {
            slug = slug[..64].Trim('-');
        }

        return string.IsNullOrWhiteSpace(slug) ? "post" : slug;
    }

    private record MicropubEntry(
        string Type,
        string? Name,
        string? Content,
        string? Summary,
        IReadOnlyList<string> Categories,
        DateTimeOffset? Published);

    private record MicropubError(
        [property: System.Text.Json.Serialization.JsonPropertyName("error")] string Error,
        [property: System.Text.Json.Serialization.JsonPropertyName("error_description")] string ErrorDescription);
}
