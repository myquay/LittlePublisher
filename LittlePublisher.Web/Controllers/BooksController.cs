using System.Text.Json;
using LittlePublisher.Web.Services.Publishing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LittlePublisher.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/books")]
[EnableRateLimiting("books")]
public sealed class BooksController(OpenLibraryService books) : ControllerBase
{
    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length is < 2 or > 200)
            return BadRequest(new { message = "Enter a title, author, or ISBN between 2 and 200 characters." });
        try { return Ok(await books.SearchAsync(q, cancellationToken)); }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or InvalidOperationException ||
            ex is OperationCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return StatusCode(503, new { message = "Open Library is unavailable or busy. Try again shortly, or enter the book manually." });
        }
    }
}
