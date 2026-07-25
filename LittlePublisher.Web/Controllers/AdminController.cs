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
    private readonly IPostStorage _postStorage;
    private readonly IWebsiteRepository _websiteRepository;
    private readonly IContentImportService _contentImportService;

    public AdminController(
        IPublisherStorage storage,
        IPostStorage postStorage,
        IWebsiteRepository websiteRepository,
        IContentImportService contentImportService)
    {
        _storage = storage;
        _postStorage = postStorage;
        _websiteRepository = websiteRepository;
        _contentImportService = contentImportService;
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        try
        {
            var jobs = await _storage.GetRecentPublishJobsAsync(10, cancellationToken);
            var posts = await _postStorage.GetRecentPostsAsync(50, cancellationToken);

            return Ok(new AdminDashboardResponse(jobs, posts));
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
        catch (Exception ex) when (ex is InvalidOperationException or IOException or System.ComponentModel.Win32Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new AdminCheckResponse(false, ex.Message));
        }
    }

    [HttpPost("import/repository")]
    public async Task<IActionResult> ImportRepository([FromBody] ImportRepositoryRequest? request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _contentImportService.ImportRepositoryAsync(request ?? new ImportRepositoryRequest(), cancellationToken);

            return Ok(result);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Azure.RequestFailedException or IOException)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new AdminCheckResponse(false, ex.Message));
        }
    }

    private record AdminDashboardResponse(
        IReadOnlyList<PublishJobRecord> Jobs,
        IReadOnlyList<PostRecord> Posts);

    private record AdminCheckResponse(bool Ok, string Message);
}
