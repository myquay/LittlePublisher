using System.Net;
using System.Net.Sockets;
using AspNet.Security.IndieAuth;
using AspNet.Security.IndieAuth.Infrastructure;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Controllers;

/// <summary>
/// Handles authentication via IndieAuth and JWT token issuance.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppConfiguration _config;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(AppConfiguration config, IJwtTokenService jwtTokenService)
    {
        _config = config;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Initiates IndieAuth login for the configured website.
    /// </summary>
    /// <returns>Challenge result that redirects to IndieAuth provider.</returns>
    [HttpGet("login")]
    public IActionResult Login()
    {
        var canonicalMe = GetConfiguredMe();

        if (string.IsNullOrEmpty(canonicalMe))
        {
            return Redirect("/login?error=Website+is+not+configured");
        }

        // Validate the URL
        try
        {
            if (!Uri.IsWellFormedUriString(canonicalMe, UriKind.Absolute) ||
                Dns.GetHostEntry(new Uri(canonicalMe).Host).AddressList.Length == 0)
            {
                return Redirect("/login?error=You+must+provide+a+valid+URL");
            }
        }
        catch (SocketException)
        {
            return Redirect("/login?error=You+must+provide+a+valid+URL");
        }

        var properties = new IndieAuthChallengeProperties
        {
            Me = canonicalMe,
            Scope = new[] { "profile", "create" },
            RedirectUri = Url.Action(nameof(Callback), "Auth", null, Request.Scheme)
        };

        return Challenge(properties, IndieAuthDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handles IndieAuth callback and issues JWT token.
    /// </summary>
    /// <returns>Redirect to frontend with JWT token.</returns>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback()
    {
        var result = await HttpContext.AuthenticateAsync(IndieAuthDefaults.AuthenticationScheme);

        if (!result.Succeeded)
        {
            // Redirect to frontend with error
            return Redirect($"/login?error={Uri.EscapeDataString("Authentication failed")}");
        }

        var me = result.Principal?.FindFirst(IndieAuthClaims.ME)?.Value;
        var configuredMe = GetConfiguredMe();

        if (string.IsNullOrEmpty(me))
        {
            return Redirect($"/login?error={Uri.EscapeDataString("Could not determine user identity")}");
        }

        if (!string.Equals(me.Canonicalize(), configuredMe, StringComparison.OrdinalIgnoreCase))
        {
            return Redirect($"/login?error={Uri.EscapeDataString("Authenticated website does not match this LittlePublisher instance")}");
        }

        var token = _jwtTokenService.GenerateToken(me, result.Principal?.Claims);

        // Redirect to frontend callback with token
        return Redirect($"/callback?token={Uri.EscapeDataString(token)}");
    }

    /// <summary>
    /// Returns current authenticated user info.
    /// </summary>
    /// <returns>User information.</returns>
    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var me = User.FindFirst(IndieAuthClaims.ME)?.Value;
        return Ok(new { me });
    }

    private string? GetConfiguredMe()
    {
        return _config.Website.Url.Canonicalize();
    }
}
