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
