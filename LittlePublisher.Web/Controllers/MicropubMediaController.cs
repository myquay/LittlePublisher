using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Publishing;
using AspNet.Security.IndieAuth.Infrastructure;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

[ApiController]
[Route("micropub/media")]
[Authorize(Policy = "MicropubToken")]
[RequestSizeLimit(MaximumUploadBytes)]
public sealed class MicropubMediaController : ControllerBase
{
    private const long MaximumUploadBytes = 20 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> SupportedTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/gif"] = ".gif",
            ["image/webp"] = ".webp",
            ["audio/mpeg"] = ".mp3",
            ["audio/ogg"] = ".ogg",
            ["video/mp4"] = ".mp4",
            ["video/webm"] = ".webm"
        };

    private readonly AppConfiguration _config;
    private readonly IWebsiteRepository _repository;

    public MicropubMediaController(AppConfiguration config, IWebsiteRepository repository)
    {
        _config = config;
        _repository = repository;
    }

    [HttpPost]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var authenticatedMe = User.FindFirst(IndieAuthClaims.ME)?.Value ??
            User.FindFirst("me")?.Value ??
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!string.Equals(
                authenticatedMe?.TrimEnd('/'),
                _config.Website.Url.TrimEnd('/'),
                StringComparison.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        if (!HasScope("media"))
        {
            Response.Headers.WWWAuthenticate = "Bearer error=\"insufficient_scope\", scope=\"media\"";
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                error = "insufficient_scope",
                error_description = "The media scope is required."
            });
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "invalid_request", error_description = "A non-empty file is required." });
        }

        if (file.Length > MaximumUploadBytes)
        {
            return StatusCode(StatusCodes.Status413PayloadTooLarge, new
            {
                error = "invalid_request",
                error_description = "The file exceeds the 20 MB upload limit."
            });
        }

        if (!SupportedTypes.TryGetValue(file.ContentType, out var extension))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, new
            {
                error = "invalid_request",
                error_description = "The uploaded media type is not supported."
            });
        }

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
            !HasExpectedImageSignature(file.ContentType, bytes))
        {
            return StatusCode(StatusCodes.Status415UnsupportedMediaType, new
            {
                error = "invalid_request",
                error_description = "The file contents do not match the declared image type."
            });
        }
        var name = $"{DateTimeOffset.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
        var repositoryPath = $"blog/static/media/{name}";
        await _repository.MutateFilesAsync(
            [RepositoryFileMutation.UpsertBinary(repositoryPath, bytes)],
            $"Upload Micropub media {Path.GetFileName(name)}",
            cancellationToken);

        var url = $"{_config.Website.Url.TrimEnd('/')}/media/{name}";
        Response.Headers.Location = url;
        return Created(url, new { url });
    }

    private bool HasScope(string requiredScope)
    {
        return User.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(scope => string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasExpectedImageSignature(string contentType, byte[] bytes)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            "image/png" => bytes.Length >= 8 &&
                bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            "image/gif" => bytes.Length >= 6 &&
                (bytes.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || bytes.AsSpan(0, 6).SequenceEqual("GIF89a"u8)),
            "image/webp" => bytes.Length >= 12 &&
                bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}
