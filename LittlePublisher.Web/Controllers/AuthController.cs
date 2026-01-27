using System.Net;
using System.Net.Sockets;
using AspNet.Security.IndieAuth;
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
    private readonly IJwtTokenService _jwtTokenService;

    public AuthController(IJwtTokenService jwtTokenService)
    {
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Initiates IndieAuth login flow.
    /// </summary>
    /// <param name="me">The user's domain URL.</param>
    /// <returns>Challenge result that redirects to IndieAuth provider.</returns>
    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? me)
    {
        if (string.IsNullOrWhiteSpace(me))
        {
            return Redirect("/login?error=me+parameter+is+required");
        }

        var canonicalMe = me.Canonicalize();

        if (string.IsNullOrEmpty(canonicalMe))
        {
            return Redirect("/login?error=Invalid+domain+format");
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

        var me = result.Principal?.FindFirst("me")?.Value;

        if (string.IsNullOrEmpty(me))
        {
            return Redirect($"/login?error={Uri.EscapeDataString("Could not determine user identity")}");
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
        var me = User.FindFirst("me")?.Value;
        return Ok(new { me });
    }
}

