using LittlePublisher.Web.Services.Publishing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/media")]
[RequestSizeLimit(MediaPublicationService.MaximumRequestBytes)]
public sealed class AdminMediaController : ControllerBase
{
    private readonly MediaPublicationService _media;

    public AdminMediaController(MediaPublicationService media)
    {
        _media = media;
    }

    [Authorize(Policy = "StagedMedia")]
    [HttpGet("staged/{id}")]
    public async Task<IActionResult> Preview(string id, CancellationToken cancellationToken)
    {
        var media = await _media.GetStagedAsync(id, cancellationToken);
        if (media is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(media.Content, media.ContentType);
    }

    [HttpPost]
    public async Task<IActionResult> Upload([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return BadRequest(new { message = "A non-empty file is required." });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _media.PublishAsync(file.ContentType, file.Length, stream, cancellationToken);
            return Created(result.Url, new { result.Url, result.RepositoryPath });
        }
        catch (MediaPublicationException ex)
        {
            return StatusCode(ex.StatusCode, new { message = ex.Message });
        }
    }
}
