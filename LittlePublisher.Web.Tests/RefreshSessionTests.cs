using System.Security.Claims;
using LittlePublisher.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace LittlePublisher.Web.Tests;

public class RefreshSessionTests
{
    [Theory]
    [InlineData(60, true)]
    [InlineData(-60, false)]
    public async Task ProtectedCookie_EnforcesTicketExpiry(int expiresInMinutes, bool succeeds)
    {
        using var services = CreateServices();
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(RefreshSession.Scheme);
        var ticket = new AuthenticationTicket(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("me", "https://example.com/")], RefreshSession.Scheme)),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(expiresInMinutes) },
            RefreshSession.Scheme);
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        context.Request.Headers.Cookie = $"LittlePublisher.Refresh={options.TicketDataFormat.Protect(ticket)}";

        var result = await context.AuthenticateAsync(RefreshSession.Scheme);

        Assert.Equal(succeeds, result.Succeeded);
    }

    [Fact]
    public async Task SignIn_ProtectsCookieAndUsesAbsoluteSevenDayLifetime()
    {
        using var services = CreateServices();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = "https";
        await context.SignInAsync(RefreshSession.Scheme,
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("me", "https://example.com/")], RefreshSession.Scheme)),
            new AuthenticationProperties { IsPersistent = true, AllowRefresh = false });
        var cookie = context.Response.Headers.SetCookie.ToString();
        Assert.Contains("httponly", cookie);
        Assert.Contains("secure", cookie);
        Assert.Contains("samesite=strict", cookie);
        Assert.Contains("path=/api/auth", cookie);
        var options = services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(RefreshSession.Scheme);
        var value = cookie.Split(';')[0].Split('=', 2)[1];
        var ticket = options.TicketDataFormat.Unprotect(value)!;
        Assert.Equal(TimeSpan.FromDays(7), ticket.Properties.ExpiresUtc - ticket.Properties.IssuedUtc);
        Assert.False(options.SlidingExpiration);
        Assert.False(ticket.Properties.AllowRefresh);
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddAuthentication().AddCookie(RefreshSession.Scheme, RefreshSession.Configure);
        return services.BuildServiceProvider();
    }
}
