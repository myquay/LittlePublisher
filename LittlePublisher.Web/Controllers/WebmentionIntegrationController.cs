using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Webmentions;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

[ApiController]
[Route("api/integrations/site-deployments")]
public sealed class WebmentionIntegrationController : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly AppConfiguration _config;
    private readonly IWebmentionDeploymentService _service;
    public WebmentionIntegrationController(AppConfiguration config, IWebmentionDeploymentService service) { _config = config; _service = service; }

    [HttpPost]
    [RequestSizeLimit(5_242_880)]
    public async Task<IActionResult> Ingest(CancellationToken cancellationToken)
    {
        if (!_config.Webmention.Enabled) return NotFound();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync(cancellationToken);
        if (!Request.Headers.TryGetValue("X-LittlePublisher-Timestamp", out var timestampHeader) || !long.TryParse(timestampHeader, out var timestamp) || Math.Abs(DateTimeOffset.UtcNow.ToUnixTimeSeconds() - timestamp) > 300)
            return Unauthorized(new { error = "Missing or stale deployment timestamp." });
        if (!Request.Headers.TryGetValue("X-LittlePublisher-Signature", out var signature) || !VerifySignature(timestampHeader!, body, signature!))
            return Unauthorized(new { error = "Invalid deployment signature." });
        DeploymentManifest? manifest;
        try { manifest = JsonSerializer.Deserialize<DeploymentManifest>(body, Json); }
        catch (JsonException) { return BadRequest(new { error = "Invalid manifest JSON." }); }
        if (manifest is null) return BadRequest(new { error = "Manifest is required." });
        await _service.IngestAsync(manifest, cancellationToken);
        return Accepted(new { manifest.Repository, manifest.CommitSha, pages = manifest.Pages.Count });
    }

    private bool VerifySignature(string timestamp, string body, string supplied)
    {
        var secret = _config.Webmention.DeploymentWebhookSecret;
        if (secret.Length < 32) return false;
        var expected = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"))).ToLowerInvariant();
        var normalized = supplied.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase) ? supplied[7..] : supplied;
        return normalized.Length == expected.Length && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(normalized.ToLowerInvariant()), Encoding.ASCII.GetBytes(expected));
    }
}
