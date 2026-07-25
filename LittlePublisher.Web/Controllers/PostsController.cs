using System.Security.Claims;
using System.Text.Json;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

[ApiController]
[Authorize]
[Route("api/posts")]
public class PostsController : ControllerBase
{
    private readonly IPostStorage _storage;
    private readonly IPublisherStorage _publisherStorage;
    private readonly IPostPublicationService _publication;

    public PostsController(
        IPostStorage storage,
        IPublisherStorage publisherStorage,
        IPostPublicationService publication)
    {
        _storage = storage;
        _publisherStorage = publisherStorage;
        _publication = publication;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 50, CancellationToken cancellationToken = default)
    {
        return Ok(await _storage.GetRecentPostsAsync(take, cancellationToken));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(string id, CancellationToken cancellationToken)
    {
        var post = await _storage.GetPostAsync(id, cancellationToken);
        return post is null ? NotFound() : Ok(post);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] SavePostRequest request, CancellationToken cancellationToken)
    {
        var validation = Validate(request);
        if (validation is not null)
        {
            return BadRequest(new ApiError(validation));
        }

        var post = await _storage.CreatePostAsync(
            new NewPost(
                request.Title,
                request.Content,
                request.Summary,
                request.Categories ?? [],
                PublishingService.BuildSlug(request.Slug ?? request.Title, request.Content),
                NormalizePostType(request.PostType, request.Title),
                request.RequestedPublishedUtc),
            cancellationToken);

        return CreatedAtAction(nameof(Get), new { id = post.Id }, post);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] SavePostRequest request,
        [FromHeader(Name = "If-Match")] string? expectedETag,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedETag))
        {
            return StatusCode(StatusCodes.Status428PreconditionRequired, new ApiError("If-Match is required."));
        }

        var validation = Validate(request);
        if (validation is not null)
        {
            return BadRequest(new ApiError(validation));
        }

        try
        {
            var post = await _storage.UpdatePostAsync(
                id,
                new PostUpdate(
                    request.Title,
                    request.Content,
                    request.Summary,
                    request.Categories ?? [],
                    PublishingService.BuildSlug(request.Slug ?? request.Title, request.Content),
                    NormalizePostType(request.PostType, request.Title),
                    request.RequestedPublishedUtc),
                expectedETag,
                cancellationToken);
            return Ok(post);
        }
        catch (Azure.RequestFailedException ex) when (ex.Status == StatusCodes.Status412PreconditionFailed)
        {
            return Conflict(new ApiError("The post changed since it was loaded. Reload it before saving."));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new ApiError(ex.Message));
        }
    }

    [HttpPost("{id}/publish")]
    public async Task<IActionResult> Publish(string id, CancellationToken cancellationToken)
    {
        if (await _storage.GetPostAsync(id, cancellationToken) is null)
        {
            return NotFound(new ApiError($"Post '{id}' was not found."));
        }

        PublishJobRecord? job = null;
        try
        {
            job = await _publisherStorage.CreatePublishJobAsync(
                new NewPublishJob(
                    UserMe: User.FindFirst("me")?.Value ??
                        User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
                        "authenticated-user",
                    ClientId: "littlepublisher-admin",
                    Action: "publish",
                    RequestJson: JsonSerializer.Serialize(new { postId = id })),
                cancellationToken);
            var post = await _publication.PublishAsync(id, cancellationToken);
            await _publisherStorage.CompletePublishJobAsync(
                job.Id,
                post.PublishedUrl ?? string.Empty,
                cancellationToken);
            return Ok(post);
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or Azure.RequestFailedException)
        {
            if (job is not null)
            {
                await _publisherStorage.FailPublishJobAsync(job.Id, ex.Message, cancellationToken);
            }

            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ApiError(ex.Message));
        }
    }

    private static string? Validate(SavePostRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content) && string.IsNullOrWhiteSpace(request.Title))
        {
            return "A post requires content or a title.";
        }

        return null;
    }

    private static string NormalizePostType(string? postType, string? title)
    {
        if (string.Equals(postType, "article", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(postType, "note", StringComparison.OrdinalIgnoreCase))
        {
            return postType!.ToLowerInvariant();
        }

        return string.IsNullOrWhiteSpace(title) ? "note" : "article";
    }

    public record SavePostRequest(
        string? Title,
        string Content,
        string? Summary,
        IReadOnlyList<string>? Categories,
        string? Slug,
        string? PostType,
        DateTimeOffset? RequestedPublishedUtc);

    private record ApiError(string Message);
}
