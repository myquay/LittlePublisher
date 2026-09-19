using Microsoft.AspNetCore.Authentication.Cookies;

namespace LittlePublisher.Web.Authentication;

public static class RefreshSession
{
    public const string Scheme = "RefreshSession";

    public static void Configure(CookieAuthenticationOptions options)
    {
        // Used only by /api/auth/refresh, never to authorize API operations.
        options.Cookie.Name = "LittlePublisher.Refresh";
        options.Cookie.Path = "/api/auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = false;
    }
}
