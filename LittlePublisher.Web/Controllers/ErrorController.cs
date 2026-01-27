using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

/// <summary>
/// Handles error responses.
/// </summary>
[ApiController]
public class ErrorController : ControllerBase
{
    /// <summary>
    /// Global error handler endpoint.
    /// </summary>
    [Route("/error")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult HandleError() => Problem();
}
