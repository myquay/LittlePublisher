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
[RequestSizeLimit(MediaPublicationService.MaximumRequestBytes)]
public sealed class MicropubMediaController : ControllerBase
{
    private readonly AppConfiguration _config;
    private readonly MediaPublicationService _media;

    public MicropubMediaController(AppConfiguration config, MediaPublicationService media)
    {
        _config = config;
        _media = media;
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

        if (file is null)
        {
            return BadRequest(new { error = "invalid_request", error_description = "A non-empty file is required." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _media.PublishAsync(file.ContentType, file.Length, stream, cancellationToken);
            Response.Headers.Location = result.Url;
            return Created(result.Url, new { url = result.Url });
        }
        catch (MediaPublicationException ex)
        {
            return StatusCode(ex.StatusCode, new
            {
                error = "invalid_request",
                error_description = ex.Message
            });
        }
    }

    private bool HasScope(string requiredScope)
    {
        return User.FindAll("scope")
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Any(scope => string.Equals(scope, requiredScope, StringComparison.OrdinalIgnoreCase));
    }

}
