using LittlePublisher.Web.Services.Webmentions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public sealed class AdminWebmentionsController : ControllerBase
{
    private readonly IWebmentionStorage _storage;
    private readonly IIncomingWebmentionService _incoming;
    private readonly IOutgoingWebmentionService _outgoing;
    private readonly IWebmentionContentService _content;
    private readonly IWebmentionQueue _queue;
    public AdminWebmentionsController(IWebmentionStorage storage, IIncomingWebmentionService incoming, IOutgoingWebmentionService outgoing, IWebmentionContentService content, IWebmentionQueue queue)
    { _storage = storage; _incoming = incoming; _outgoing = outgoing; _content = content; _queue = queue; }

    [HttpGet("webmentions/incoming")]
    public async Task<IActionResult> Incoming([FromQuery] string? state, [FromQuery] int take = 100, CancellationToken cancellationToken = default) => Ok(new WebmentionListResponse<IncomingWebmentionRecord>(await _storage.ListIncomingAsync(state, take, cancellationToken)));

    [HttpGet("webmentions/incoming/{id}")]
    public async Task<IActionResult> IncomingDetail(string id, CancellationToken cancellationToken)
    {
        var item = await _storage.GetIncomingAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("webmentions/incoming/{id}/approve")]
    public async Task<IActionResult> Approve(string id, CancellationToken cancellationToken) => Ok(await _incoming.ApproveAsync(id, cancellationToken));

    [HttpPost("webmentions/incoming/{id}/reject")]
    public async Task<IActionResult> Reject(string id, [FromBody] RejectWebmentionRequest? request, CancellationToken cancellationToken) => Ok(await _incoming.RejectAsync(id, request?.Reason, request?.BlockDomain ?? false, cancellationToken));

    [HttpPost("webmentions/incoming/{id}/reverify")]
    public async Task<IActionResult> Reverify(string id, CancellationToken cancellationToken) { await _incoming.ReverifyAsync(id, cancellationToken); return Accepted(); }

    [HttpDelete("webmentions/incoming/{id}")]
    public async Task<IActionResult> DeleteIncoming(string id, CancellationToken cancellationToken)
    {
        var item = await _storage.GetIncomingAsync(id, cancellationToken);
        if (item is null) return NotFound();
        if (!string.IsNullOrWhiteSpace(item.RepositoryPath) || item.State == WebmentionStates.Approved) return BadRequest(new { error = "Withdraw an approved Webmention before deleting its private moderation record." });
        await _storage.DeleteIncomingAsync(id, cancellationToken); return NoContent();
    }

    [HttpGet("webmentions/outgoing")]
    public async Task<IActionResult> Outgoing([FromQuery] string? state, [FromQuery] int take = 100, CancellationToken cancellationToken = default) => Ok(new WebmentionListResponse<OutgoingWebmentionRecord>(await _storage.ListOutgoingAsync(state, take, cancellationToken)));

    [HttpGet("webmentions/outgoing/{id}")]
    public async Task<IActionResult> OutgoingDetail(string id, CancellationToken cancellationToken)
    {
        var item = await _storage.GetOutgoingAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(new { item, attempts = await _storage.ListAttemptsAsync(id, cancellationToken) });
    }

    [HttpPost("webmentions/outgoing/{id}/send")]
    [HttpPost("webmentions/outgoing/{id}/retry")]
    public async Task<IActionResult> Send(string id, CancellationToken cancellationToken) { await _outgoing.QueueSendAsync(id, cancellationToken); return Accepted(); }

    [HttpPost("webmentions/outgoing/{id}/decline")]
    public async Task<IActionResult> Decline(string id, CancellationToken cancellationToken) => Ok(await _outgoing.DeclineAsync(id, cancellationToken));

    [HttpPost("webmentions/outgoing/scan")]
    public async Task<IActionResult> Scan([FromBody] ScanPageRequest request, CancellationToken cancellationToken) { await _outgoing.ScanAsync(request.SourceUrl, request.ForceResend, cancellationToken); return Accepted(); }

    [HttpGet("site-pages")]
    public async Task<IActionResult> SitePages([FromQuery] int take = 100, CancellationToken cancellationToken = default) => Ok(new WebmentionListResponse<SitePageRecord>(await _storage.ListSitePagesAsync(take, cancellationToken)));

    [HttpPost("content/likes")]
    public async Task<IActionResult> Like([FromBody] CreateLikeRequest request, CancellationToken cancellationToken) => Ok(await _content.CreateLikeAsync(request, cancellationToken));

    [HttpPost("content/replies")]
    public async Task<IActionResult> Reply([FromBody] CreateReplyRequest request, CancellationToken cancellationToken) => Ok(await _content.CreateReplyAsync(request, cancellationToken));

    [HttpPost("blogroll")]
    public async Task<IActionResult> Blogroll([FromBody] CreateBlogrollRequest request, CancellationToken cancellationToken) => Ok(await _content.CreateBlogrollAsync(request, cancellationToken));

    [HttpPost("checks/webmentions")]
    public async Task<IActionResult> Check(CancellationToken cancellationToken)
    {
        await _storage.CheckHealthAsync(cancellationToken); await _queue.CheckHealthAsync(cancellationToken);
        return Ok(new { ok = true, message = "Webmention tables and queues are reachable." });
    }
}
