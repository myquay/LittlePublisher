using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly IPublisherStorage _storage;
    private readonly IWebsiteRepository _websiteRepository;

    public AdminController(IPublisherStorage storage, IWebsiteRepository websiteRepository)
    {
        _storage = storage;
        _websiteRepository = websiteRepository;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        try
        {
            var jobs = await _storage.GetRecentPublishJobsAsync(10, cancellationToken);
            var items = await _storage.GetRecentPublishedItemsAsync(10, cancellationToken);

            return Ok(new AdminDashboardResponse(jobs, items));
        }
        catch (Exception ex) when (ex is InvalidOperationException or Azure.RequestFailedException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new AdminCheckResponse(false, ex.Message));
        }
    }

    [HttpPost("checks/storage")]
    public async Task<IActionResult> CheckStorage(CancellationToken cancellationToken)
    {
        try
        {
            await _storage.CheckHealthAsync(cancellationToken);
            return Ok(new AdminCheckResponse(true, "Storage is reachable."));
        }
        catch (Exception ex) when (ex is InvalidOperationException or Azure.RequestFailedException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new AdminCheckResponse(false, ex.Message));
        }
    }

    [HttpPost("checks/github")]
    public async Task<IActionResult> CheckGitHub(CancellationToken cancellationToken)
    {
        try
        {
            await _websiteRepository.CheckConnectionAsync(cancellationToken);
            return Ok(new AdminCheckResponse(true, "GitHub repository is reachable."));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new AdminCheckResponse(false, ex.Message));
        }
    }

    private record AdminDashboardResponse(
        IReadOnlyList<PublishJobRecord> Jobs,
        IReadOnlyList<PublishedItemRecord> Items);

    private record AdminCheckResponse(bool Ok, string Message);
}
