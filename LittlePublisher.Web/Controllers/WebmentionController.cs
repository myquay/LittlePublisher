using LittlePublisher.Web.Services.Webmentions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LittlePublisher.Web.Controllers;

[ApiController]
[EnableRateLimiting("webmention")]
public sealed class WebmentionController : ControllerBase
{
    private readonly IIncomingWebmentionService _service;
    public WebmentionController(IIncomingWebmentionService service) => _service = service;

    [HttpPost("/webmention")]
    [Consumes("application/x-www-form-urlencoded")]
    [RequestSizeLimit(16_384)]
    public async Task<IActionResult> Receive([FromForm] string? source, [FromForm] string? target, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target)) return BadRequest(new { error = "source and target are required." });
        try
        {
            await _service.ReceiveAsync(source, target, cancellationToken);
            return Accepted();
        }
        catch (WebmentionDisabledException) { return NotFound(); }
        catch (WebmentionBlockedException) { return Accepted(); }
        catch (WebmentionRequestException ex) { return BadRequest(new { error = ex.Message }); }
        catch (Azure.RequestFailedException) { return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = "The Webmention could not be queued." }); }
    }
}
