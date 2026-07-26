using System.Security.Claims;
using System.Text.Json;
using AspNet.Security.IndieAuth;
using AspNet.Security.IndieAuth.Infrastructure;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

[ApiController]
[Route("micropub")]
public class MicropubController : ControllerBase
{
    private const string ExternalMicropubTokenScheme = "ExternalMicropubToken";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppConfiguration _config;
    private readonly IPublisherStorage _storage;
    private readonly IPostStorage _postStorage;
    private readonly IPostPublicationService _publicationService;

    public MicropubController(
        AppConfiguration config,
        IPublisherStorage storage,
        IPostStorage postStorage,
        IPostPublicationService publicationService)
    {
        _config = config;
        _storage = storage;
        _postStorage = postStorage;
        _publicationService = publicationService;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(
        [FromQuery] string? q,
        [FromQuery] string? url,
        [FromQuery] string? type,
        [FromQuery] string? status,
        [FromQuery] int take,
        CancellationToken cancellationToken)
    {
        return q switch
        {
            "config" => Ok(GetConfigResponse()),
            "source" => await GetSourceAsync(url, cancellationToken),
            "posts" => await GetPostsAsync(type, status, take, cancellationToken),
            _ => BadRequest(new MicropubError("invalid_request", "Unsupported Micropub query."))
        };
    }

    [NonAction]
    public Task<IActionResult> Get(string? q, string? url, CancellationToken cancellationToken) =>
        Get(q, url, null, null, 50, cancellationToken);

    [HttpPost]
    [Authorize(Policy = "MicropubToken")]
    public async Task<IActionResult> Post(CancellationToken cancellationToken)
    {
        if (!IsConfiguredUser())
        {
            return Forbid();
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

        var requiredScope = entry.Action?.ToLowerInvariant() switch
        {
            "update" => "update",
            "delete" or "undelete" => "delete",
            _ => "create"
        };
        if (!HasScope(requiredScope))
        {
            Response.Headers.WWWAuthenticate = $"Bearer error=\"insufficient_scope\", scope=\"{requiredScope}\"";
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new MicropubError("insufficient_scope", $"The {requiredScope} scope is required."));
        }

        if (string.Equals(entry.Action, "delete", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(entry.Action, "undelete", StringComparison.OrdinalIgnoreCase))
        {
            return await SetDeletedAsync(
                entry,
                deleted: string.Equals(entry.Action, "delete", StringComparison.OrdinalIgnoreCase),
                cancellationToken);
        }

        if (string.Equals(entry.Action, "update", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                return await UpdateAsync(entry, cancellationToken);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Azure.RequestFailedException or IOException)
            {
                return StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    new MicropubError("temporarily_unavailable", ex.Message));
            }
        }

        if (!string.Equals(entry.Type, "entry", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new MicropubError("invalid_request", "Only h=entry create requests are supported."));
        }

        if (string.IsNullOrWhiteSpace(entry.Content) &&
            string.IsNullOrWhiteSpace(entry.Name) &&
            entry.Properties.Count == 0)
        {
            return BadRequest(new MicropubError("invalid_request", "A Micropub entry requires content or name."));
        }

        var content = entry.Content ?? entry.Name ?? string.Empty;
        PublishJobRecord? job = null;
        PostRecord? post = null;

        try
        {
            post = await _postStorage.CreatePostAsync(
                new NewPost(
                    Title: entry.Name,
                    Content: content,
                    Summary: entry.Summary,
                    Categories: entry.Categories,
                    Slug: PublishingService.BuildSlug(entry.Name, BuildSlugContent(entry, content)),
                    PostType: ResolvePostType(entry),
                    RequestedPublishedUtc: entry.Published,
                    Properties: entry.Properties),
                cancellationToken);

            job = await _storage.CreatePublishJobAsync(
                new NewPublishJob(
                    UserMe: GetAuthenticatedMe()!,
                    ClientId: User.FindFirst("client_id")?.Value,
                    Action: string.Equals(entry.PostStatus, "draft", StringComparison.OrdinalIgnoreCase)
                        ? "create-draft"
                        : "publish",
                    RequestJson: JsonSerializer.Serialize(
                        new { postId = post.Id, postStatus = entry.PostStatus ?? "published" },
                        JsonOptions)),
                cancellationToken);

            if (!string.Equals(entry.PostStatus, "draft", StringComparison.OrdinalIgnoreCase))
            {
                post = await _publicationService.PublishAsync(post.Id, cancellationToken);
            }

            var location = post.PublishedUrl ?? BuildManagementUrl(post.Id);
            await _storage.CompletePublishJobAsync(job.Id, location, cancellationToken);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Azure.RequestFailedException or IOException)
        {
            if (job is not null)
            {
                await _storage.FailPublishJobAsync(job.Id, ex.Message, cancellationToken);
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new MicropubError("temporarily_unavailable", ex.Message));
        }

        var resultUrl = post!.PublishedUrl ?? BuildManagementUrl(post.Id);
        Response.Headers.Location = resultUrl;

        return Created(resultUrl, new { url = resultUrl });
    }

    private async Task<IActionResult> UpdateAsync(MicropubEntry entry, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.Url))
        {
            return BadRequest(new MicropubError("invalid_request", "An update requires a url."));
        }

        var post = await ResolvePostAsync(entry.Url, cancellationToken);
        if (post is null)
        {
            return NotFound(new MicropubError("not_found", "The requested post was not found."));
        }

        var authoringProperties = new[]
        {
            "name", "content", "summary", "category", "published", "post-type",
            "photo", "alt", "location", "in-reply-to", "like-of", "repost-of",
            "bookmark-of", "url", "feed", "start", "end", "audio", "video"
        };
        var changedProperties = entry.ProvidedProperties
            .Concat(entry.AddProperties.Keys)
            .Concat(entry.DeleteProperties)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (authoringProperties.Any(changedProperties.Contains))
        {
            var categories = entry.DeleteProperties.Contains("category")
                ? Array.Empty<string>()
                : entry.ProvidedProperties.Contains("category")
                    ? entry.Categories
                    : entry.AddProperties.TryGetValue("category", out var addedCategories)
                        ? post.Categories.Concat(addedCategories).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                        : post.Categories;
            post = await _postStorage.UpdatePostAsync(
                post.Id,
                new PostUpdate(
                    Title: entry.DeleteProperties.Contains("name")
                        ? null
                        : entry.ProvidedProperties.Contains("name") ? entry.Name : post.Title,
                    Content: entry.DeleteProperties.Contains("content")
                        ? string.Empty
                        : entry.ProvidedProperties.Contains("content") ? entry.Content ?? string.Empty : post.Content,
                    Summary: entry.DeleteProperties.Contains("summary")
                        ? null
                        : entry.ProvidedProperties.Contains("summary") ? entry.Summary : post.Summary,
                    Categories: categories,
                    Slug: post.Slug,
                    PostType: changedProperties.Contains("post-type") ||
                        entry.Properties.Keys.Any(IsRelationshipProperty)
                        ? ResolvePostType(entry)
                        : post.PostType,
                    RequestedPublishedUtc: entry.DeleteProperties.Contains("published")
                        ? null
                        : entry.ProvidedProperties.Contains("published") ? entry.Published : post.RequestedPublishedUtc,
                    Properties: MergeProperties(
                        post.MicropubProperties,
                        entry.Properties,
                        entry.ProvidedProperties,
                        entry.AddProperties,
                        entry.DeleteProperties)),
                post.ETag,
                cancellationToken);
        }

        if (string.Equals(entry.PostStatus, "published", StringComparison.OrdinalIgnoreCase))
        {
            await _publicationService.PublishAsync(post.Id, cancellationToken);
        }

        return Ok();
    }

    private async Task<IActionResult> SetDeletedAsync(
        MicropubEntry entry,
        bool deleted,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(entry.Url))
        {
            return BadRequest(new MicropubError("invalid_request", "A delete or undelete action requires a url."));
        }

        var post = await ResolvePostAsync(entry.Url, cancellationToken);
        if (post is null)
        {
            return NotFound(new MicropubError("not_found", "The requested post was not found."));
        }

        if (deleted)
        {
            await _publicationService.DeleteAsync(post.Id, cancellationToken);
        }
        else
        {
            await _publicationService.UndeleteAsync(post.Id, cancellationToken);
        }
        return NoContent();
    }

    private async Task<IActionResult> GetPostsAsync(
        string? type,
        string? status,
        int take,
        CancellationToken cancellationToken)
    {
        if (!await TryAuthenticateMicropubTokenAsync())
        {
            return Challenge(_config.ExternalToken.Enabled &&
                _config.ExternalToken.IsSupportedMode ? [ExternalMicropubTokenScheme] : []);
        }

        if (!IsConfiguredUser())
        {
            return Forbid();
        }

        var posts = await _postStorage.GetRecentPostsAsync(Math.Clamp(take == 0 ? 50 : take, 1, 50), cancellationToken);
        var items = posts
            .Where(post => string.IsNullOrWhiteSpace(type) ||
                string.Equals(post.PostType, type, StringComparison.OrdinalIgnoreCase))
            .Where(post => string.IsNullOrWhiteSpace(status) ||
                string.Equals(post.State, status, StringComparison.OrdinalIgnoreCase) ||
                status.Equals("published", StringComparison.OrdinalIgnoreCase) && post.PublishedRevision is not null)
            .Select(post => new
            {
                type = new[] { "h-entry" },
                properties = ToSourceProperties(post, post.PublishedUrl ?? BuildManagementUrl(post.Id))["properties"],
                postType = post.PostType,
                state = post.State,
                managementUrl = BuildManagementUrl(post.Id),
                publishedUrl = post.PublishedUrl,
                updated = post.UpdatedUtc,
                revision = post.WorkingRevision,
                hasUnpublishedChanges = post.HasUnpublishedChanges
            })
            .ToArray();

        return Ok(new { items });
    }

    private async Task<IActionResult> GetSourceAsync(string? url, CancellationToken cancellationToken)
    {
        if (!await TryAuthenticateMicropubTokenAsync())
        {
            return Challenge(_config.ExternalToken.Enabled
                && _config.ExternalToken.IsSupportedMode
                ? [ExternalMicropubTokenScheme]
                : []);
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
            var item = await ResolvePostAsync(url, cancellationToken);

            if (item is null)
            {
                return NotFound(new MicropubError("not_found", "The requested source item was not found."));
            }

            var sourceUrl = item.PublishedUrl ?? BuildManagementUrl(item.Id);
            var json = JsonSerializer.Serialize(ToSourceProperties(item, sourceUrl), JsonOptions);
            return Content(json, "application/json");
        }
        catch (Exception ex) when (ex is InvalidOperationException or Azure.RequestFailedException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new MicropubError("temporarily_unavailable", ex.Message));
        }
    }

    private async Task<bool> TryAuthenticateMicropubTokenAsync()
    {
        if (!_config.ExternalToken.Enabled || !_config.ExternalToken.IsSupportedMode)
        {
            return false;
        }

        var externalResult = await HttpContext.AuthenticateAsync(ExternalMicropubTokenScheme);

        if (externalResult.Succeeded && externalResult.Principal is not null)
        {
            HttpContext.User = externalResult.Principal;
            return true;
        }

        return false;
    }

    private object GetConfigResponse()
    {
        var host = _config.Host.TrimEnd('/');

        return new Dictionary<string, object>
        {
            ["micropub"] = $"{host}/micropub",
            ["media-endpoint"] = $"{host}/micropub/media",
            ["syndicate-to"] = Array.Empty<object>(),
            ["extensions"] = new[] { "q=posts", "post-status", "delete", "undelete" },
            ["post-types"] = new[]
            {
                new { type = "note", name = "Note" },
                new { type = "article", name = "Article" },
                new { type = "photo", name = "Photo" },
                new { type = "activity", name = "Activity" },
                new { type = "reply", name = "Reply" },
                new { type = "like", name = "Like" },
                new { type = "repost", name = "Repost" },
                new { type = "bookmark", name = "Bookmark" },
                new { type = "blogroll", name = "Blogroll entry" },
                new { type = "event", name = "Event" },
                new { type = "audio", name = "Audio" },
                new { type = "video", name = "Video" }
            }
        };
    }

    private async Task<MicropubEntry> ReadEntryAsync(CancellationToken cancellationToken)
    {
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(cancellationToken);
            var provided = form.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            return new MicropubEntry(
                Action: form["action"].FirstOrDefault(),
                Url: form["url"].FirstOrDefault(),
                Type: StripHPrefix(form["h"].FirstOrDefault()),
                Name: form["name"].FirstOrDefault(),
                Content: form["content"].FirstOrDefault(),
                Summary: form["summary"].FirstOrDefault(),
                Categories: form["category"]
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .ToArray(),
                Published: ParsePublished(form["published"].FirstOrDefault()),
                PostStatus: form["post-status"].FirstOrDefault(),
                ProvidedProperties: provided,
                Properties: ReadFormProperties(form),
                AddProperties: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                DeleteProperties: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        if (Request.ContentType?.Contains("json", StringComparison.OrdinalIgnoreCase) == true)
        {
            using var document = await JsonDocument.ParseAsync(Request.Body, cancellationToken: cancellationToken);
            var root = document.RootElement;
            var action = ReadFirstString(root, "action");
            var propertyName = string.Equals(action, "update", StringComparison.OrdinalIgnoreCase) ? "replace" : "properties";
            var properties = root.TryGetProperty(propertyName, out var props) ? props : default;
            var provided = properties.ValueKind == JsonValueKind.Object
                ? properties.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.OrdinalIgnoreCase)
                : [];

            return new MicropubEntry(
                Action: action,
                Url: ReadFirstString(root, "url"),
                Type: StripHPrefix(ReadFirstString(root, "type")),
                Name: ReadFirstPropertyString(properties, "name"),
                Content: ReadContentProperty(properties),
                Summary: ReadFirstPropertyString(properties, "summary"),
                Categories: ReadPropertyStrings(properties, "category"),
                Published: ParsePublished(ReadFirstPropertyString(properties, "published")),
                PostStatus: ReadFirstPropertyString(properties, "post-status"),
                ProvidedProperties: provided,
                Properties: ReadJsonProperties(properties),
                AddProperties: root.TryGetProperty("add", out var additions)
                    ? ReadJsonProperties(additions, includeStandardProperties: true)
                    : new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase),
                DeleteProperties: ReadDeleteProperties(root));
        }

        throw new InvalidOperationException("Micropub requests must be form-encoded or JSON.");
    }

    private Dictionary<string, object> ToSourceProperties(PostRecord post, string url)
    {
        var properties = new Dictionary<string, object>
        {
            ["content"] = new[] { post.Content },
            ["url"] = new[] { url },
            ["post-status"] = new[]
            {
                post.State == PostStates.Deleted
                    ? "deleted"
                    : post.HasUnpublishedChanges ? "draft" : "published"
            },
            ["post-type"] = new[] { post.PostType }
        };

        if (post.PublishedUtc is { } publishedUtc)
        {
            properties["published"] = new[] { publishedUtc.ToString("O") };
        }

        if (!string.IsNullOrWhiteSpace(post.Title))
        {
            properties["name"] = new[] { post.Title };
        }

        if (!string.IsNullOrWhiteSpace(post.Summary))
        {
            properties["summary"] = new[] { post.Summary };
        }

        if (post.Categories.Count > 0)
        {
            properties["category"] = post.Categories;
        }

        foreach (var property in post.MicropubProperties)
        {
            if (!properties.ContainsKey(property.Key))
            {
                properties[property.Key] = property.Value;
            }
        }

        return new Dictionary<string, object>
        {
            ["type"] = new[] { "h-entry" },
            ["properties"] = properties
        };
    }

    private string BuildManagementUrl(string postId) => $"{_config.Host.TrimEnd('/')}/micropub/posts/{postId}";

    private async Task<PostRecord?> ResolvePostAsync(string url, CancellationToken cancellationToken)
    {
        var managementPrefix = $"{_config.Host.TrimEnd('/')}/micropub/posts/";
        if (url.StartsWith(managementPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return await _postStorage.GetPostAsync(url[managementPrefix.Length..].Trim('/'), cancellationToken);
        }

        return await _postStorage.GetPostByUrlAsync(url, cancellationToken);
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

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadFormProperties(IFormCollection form)
    {
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "h", "action", "url", "access_token", "name", "content", "summary",
            "category", "published", "post-status"
        };
        return form
            .Where(pair => !reserved.Contains(pair.Key) && !pair.Key.StartsWith("mp-", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                pair => pair.Key.TrimEnd('[', ']'),
                pair => (IReadOnlyList<string>)pair.Value
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Select(value => value!)
                    .ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadJsonProperties(
        JsonElement properties,
        bool includeStandardProperties = false)
    {
        if (properties.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        }

        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "name", "content", "summary", "category", "published", "post-status"
        };
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in properties.EnumerateObject())
        {
            if ((!includeStandardProperties && reserved.Contains(property.Name)) ||
                property.Name.StartsWith("mp-", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values = property.Value.ValueKind == JsonValueKind.Array
                ? property.Value.EnumerateArray().Select(ReadPropertyValue).Where(value => value is not null).Select(value => value!).ToArray()
                : [];
            if (values.Length > 0)
            {
                result[property.Name] = values;
            }
        }

        return result;
    }

    private static IReadOnlySet<string> ReadDeleteProperties(JsonElement root)
    {
        if (!root.TryGetProperty("delete", out var deletion))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        if (deletion.ValueKind == JsonValueKind.Array)
        {
            return deletion.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        if (deletion.ValueKind == JsonValueKind.Object)
        {
            return deletion.EnumerateObject()
                .Select(property => property.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    private static string? ReadPropertyValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString();
        }

        if (value.ValueKind == JsonValueKind.Object)
        {
            if (value.TryGetProperty("value", out var literal) && literal.ValueKind == JsonValueKind.String)
            {
                return literal.GetString();
            }

            if (value.TryGetProperty("html", out var html) && html.ValueKind == JsonValueKind.String)
            {
                return html.GetString();
            }
        }

        return null;
    }

    private static string ResolvePostType(MicropubEntry entry)
    {
        if (entry.Properties.TryGetValue("post-type", out var explicitTypes) &&
            explicitTypes.FirstOrDefault() is { } explicitType)
        {
            return explicitType.Trim().ToLowerInvariant();
        }

        if (entry.Properties.ContainsKey("in-reply-to")) return "reply";
        if (entry.Properties.ContainsKey("like-of")) return "like";
        if (entry.Properties.ContainsKey("repost-of")) return "repost";
        if (entry.Properties.ContainsKey("bookmark-of")) return "bookmark";
        if (entry.Properties.ContainsKey("photo")) return "photo";
        if (entry.Properties.ContainsKey("video")) return "video";
        if (entry.Properties.ContainsKey("audio")) return "audio";
        if (entry.Properties.ContainsKey("start")) return "event";
        return string.IsNullOrWhiteSpace(entry.Name) ? "note" : "article";
    }

    private static string BuildSlugContent(MicropubEntry entry, string content)
    {
        if (!string.IsNullOrWhiteSpace(content))
        {
            return content;
        }

        var propertyValue = entry.Properties
            .Where(property => property.Key is not "post-type")
            .SelectMany(property => property.Value)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return propertyValue ?? $"entry-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}";
    }

    private static bool IsRelationshipProperty(string property) =>
        property is "in-reply-to" or "like-of" or "repost-of" or "bookmark-of";

    private static IReadOnlyDictionary<string, IReadOnlyList<string>> MergeProperties(
        IReadOnlyDictionary<string, IReadOnlyList<string>> current,
        IReadOnlyDictionary<string, IReadOnlyList<string>> replacement,
        IReadOnlySet<string> provided,
        IReadOnlyDictionary<string, IReadOnlyList<string>> additions,
        IReadOnlySet<string> deletions)
    {
        var merged = new Dictionary<string, IReadOnlyList<string>>(current, StringComparer.OrdinalIgnoreCase);
        if (provided.Contains("post-type"))
        {
            foreach (var typeSpecific in new[]
            {
                "photo", "alt", "location", "in-reply-to", "like-of", "repost-of",
                "bookmark-of", "url", "feed", "start", "end", "audio", "video"
            })
            {
                if (!replacement.ContainsKey(typeSpecific))
                {
                    merged.Remove(typeSpecific);
                }
            }
        }

        foreach (var property in provided)
        {
            if (replacement.TryGetValue(property, out var values))
            {
                merged[property] = values;
            }
            else if (!property.StartsWith("mp-", StringComparison.OrdinalIgnoreCase))
            {
                merged.Remove(property);
            }
        }

        foreach (var property in additions)
        {
            merged[property.Key] = merged.TryGetValue(property.Key, out var existing)
                ? existing.Concat(property.Value).Distinct(StringComparer.Ordinal).ToArray()
                : property.Value;
        }

        foreach (var property in deletions)
        {
            merged.Remove(property);
        }

        return merged;
    }

    private static string StripHPrefix(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.StartsWith("h-", StringComparison.OrdinalIgnoreCase) ? value[2..] : value;
    }

    private record MicropubEntry(
        string? Action,
        string? Url,
        string Type,
        string? Name,
        string? Content,
        string? Summary,
        IReadOnlyList<string> Categories,
        DateTimeOffset? Published,
        string? PostStatus,
        IReadOnlySet<string> ProvidedProperties,
        IReadOnlyDictionary<string, IReadOnlyList<string>> Properties,
        IReadOnlyDictionary<string, IReadOnlyList<string>> AddProperties,
        IReadOnlySet<string> DeleteProperties);

    private record MicropubError(
        [property: System.Text.Json.Serialization.JsonPropertyName("error")] string Error,
        [property: System.Text.Json.Serialization.JsonPropertyName("error_description")] string ErrorDescription);
}
