using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AspNet.Security.IndieAuth;
using AspNet.Security.IndieAuth.Infrastructure;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Controllers;
using LittlePublisher.Web.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace LittlePublisher.Web.Tests;

public class AuthControllerTests
{
    [Fact]
    public void Login_WhenAuthenticationIsNotConfigured_RedirectsWithError()
    {
        var config = CreateConfig();
        config.Jwt.SecretKey = "CHANGE_THIS_TO_A_SECURE_KEY_AT_LEAST_32_CHARACTERS";
        var controller = CreateController(config);

        var result = Assert.IsType<RedirectResult>(controller.Login());

        Assert.Equal("/login?error=Authentication+is+not+configured", result.Url);
    }

    [Fact]
    public async Task Callback_WhenAuthenticationFails_RedirectsWithError()
    {
        var controller = CreateController(authenticateResult: AuthenticateResult.Fail("nope"));

        var result = Assert.IsType<RedirectResult>(await controller.Callback());

        Assert.Equal("/login?error=Authentication%20failed", result.Url);
    }

    [Fact]
    public async Task Callback_WhenMeClaimIsMissing_RedirectsWithError()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([], IndieAuthDefaults.AuthenticationScheme));
        var controller = CreateController(authenticateResult: AuthenticateResult.Success(new AuthenticationTicket(
            principal,
            IndieAuthDefaults.AuthenticationScheme)));

        var result = Assert.IsType<RedirectResult>(await controller.Callback());

        Assert.Contains("Could%20not%20determine%20user%20identity", result.Url);
    }

    [Fact]
    public async Task Callback_WhenAuthenticatedWebsiteDoesNotMatch_RedirectsWithError()
    {
        var principal = PrincipalWithMe("https://other.example/");
        var controller = CreateController(authenticateResult: AuthenticateResult.Success(new AuthenticationTicket(
            principal,
            IndieAuthDefaults.AuthenticationScheme)));

        var result = Assert.IsType<RedirectResult>(await controller.Callback());

        Assert.Contains("Authenticated%20website%20does%20not%20match", result.Url);
    }

    [Fact]
    public async Task Callback_WhenAuthenticatedWebsiteMatches_RedirectsWithJwtToken()
    {
        var principal = PrincipalWithMe("https://example.com/");
        var tokenService = new CapturingJwtTokenService { Token = "signed-token" };
        var controller = CreateController(
            tokenService: tokenService,
            authenticateResult: AuthenticateResult.Success(new AuthenticationTicket(
                principal,
                IndieAuthDefaults.AuthenticationScheme)));

        var result = Assert.IsType<RedirectResult>(await controller.Callback());

        Assert.Equal("/callback?token=signed-token", result.Url);
        Assert.Equal("https://example.com/", tokenService.Me);
        Assert.Contains(tokenService.Claims!, claim => claim.Type == IndieAuthClaims.ME);
    }

    [Fact]
    public void Me_ReturnsCurrentMeClaim()
    {
        var controller = CreateController();
        controller.ControllerContext.HttpContext.User = PrincipalWithMe("https://example.com/");

        var result = Assert.IsType<OkObjectResult>(controller.Me());

        Assert.Contains("https://example.com/", System.Text.Json.JsonSerializer.Serialize(result.Value));
    }

    [Fact]
    public void JwtTokenService_GeneratesTokenWithConfiguredIssuerAudienceAndClaims()
    {
        var config = CreateConfig();
        var service = new JwtTokenService(config);

        var token = service.GenerateToken("https://example.com/", [new Claim("scope", "create")]);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(config.Jwt.Issuer, jwt.Issuer);
        Assert.Contains(config.Jwt.Audience, jwt.Audiences);
        Assert.Contains(jwt.Claims, claim => claim.Type == "me" && claim.Value == "https://example.com/");
        Assert.Contains(jwt.Claims, claim => claim.Type == "scope" && claim.Value == "create");
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }

    private static AuthController CreateController(
        AppConfiguration? config = null,
        IJwtTokenService? tokenService = null,
        AuthenticateResult? authenticateResult = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(new StubAuthenticationService(
            authenticateResult ?? AuthenticateResult.NoResult()));

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        httpContext.Request.Scheme = "https";

        var controller = new AuthController(
            config ?? CreateConfig(),
            tokenService ?? new JwtTokenService(config ?? CreateConfig()))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };

        return controller;
    }

    private static AppConfiguration CreateConfig()
    {
        return new AppConfiguration
        {
            Website = new WebsiteConfiguration { Url = "https://example.com/" },
            Jwt = new JwtConfiguration
            {
                Issuer = "https://publisher.example",
                Audience = "https://publisher.example",
                SecretKey = "a-secure-jwt-secret-key-with-enough-length",
                ExpiryMinutes = 60
            }
        };
    }

    private static ClaimsPrincipal PrincipalWithMe(string me)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(IndieAuthClaims.ME, me), new Claim("scope", "create")],
            IndieAuthDefaults.AuthenticationScheme));
    }

    private sealed class CapturingJwtTokenService : IJwtTokenService
    {
        public string Token { get; init; } = "token";

        public string? Me { get; private set; }

        public IReadOnlyList<Claim>? Claims { get; private set; }

        public string GenerateToken(string me, IEnumerable<Claim>? additionalClaims = null)
        {
            Me = me;
            Claims = additionalClaims?.ToArray();

            return Token;
        }
    }

    private sealed class StubAuthenticationService : IAuthenticationService
    {
        private readonly AuthenticateResult _authenticateResult;

        public StubAuthenticationService(AuthenticateResult authenticateResult)
        {
            _authenticateResult = authenticateResult;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme)
        {
            return Task.FromResult(_authenticateResult);
        }

        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            return Task.CompletedTask;
        }
    }
}
