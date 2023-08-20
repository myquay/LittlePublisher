using IndieAuth;
using IndieAuth.Authentication;
using IndieAuth.Claims;
using LittlePublisher.Web.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Linq.Expressions;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration.GetSection("App").Get<AppConfiguration>();

var authBuilder = builder.Services.AddAuthentication()
                .AddCookie(IndieAuthDefaults.ExternalCookieSignInScheme, options =>
                {
                    options.LoginPath = "/account/sign-in";
                    
                    options.Events.OnRedirectToLogin = context =>
                    {
                        //User we are attempting to sign in as
                        if (context.Properties.Items.TryGetValue("me", out string? me) && me != null)
                            context.RedirectUri = QueryHelpers.AddQueryString(context.RedirectUri, "me", me);

                        //Client we are attempting to sign in for
                        if (context.Properties.Items.TryGetValue("client_id", out string? clientId) && clientId != null)
                            context.RedirectUri = QueryHelpers.AddQueryString(context.RedirectUri, "client_id", clientId);

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    };
                })
                .AddIndieAuth(IndieAuthDefaults.AuthenticationScheme, options =>
                {
                    options.Issuer = config.Host;
                    options.Scopes = new[] { "profile", "create", "update", "delete", "media" };

                    options.AuthorizationEndpoint = "/indie-auth/authorization";
                    options.TokenEndpoint = "/indie-auth/token";
                    options.IntrospectionEndpoint = "/indie-auth/token-info";

                    options.ExternalSignInScheme = IndieAuthDefaults.ExternalCookieSignInScheme;

                });

if (config.IndieAuth.GitHub.Enabled)
{
    authBuilder.AddGitHub(IndieAuthMethod.GITHUB, options =>
    {
        options.SignInScheme = IndieAuthDefaults.ExternalCookieSignInScheme;
        options.ClientId = config.IndieAuth.GitHub.ClientId;
        options.ClientSecret = config.IndieAuth.GitHub.ClientSecret;
        options.CallbackPath = "/indie-auth/process/github";
        options.Events.OnTicketReceived = IndieAuthClaims.OnGitHubTicketReceived; 
    });
}

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(IndieAuthDefaults.ExternalCookieSignInScheme)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRazorPages().AddRazorPagesOptions(options =>
{
    options.Conventions.AddPageRoute("/Account/SignIn", "/account/sign-in");
});

builder.Services.AddSingleton<HttpClient>((a) =>
{
    var client = new HttpClient();

    client.DefaultRequestHeaders.Add("User-Agent", "IndieAuth.NET/1.0");

    return client;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseAuthentication();

app.MapRazorPages();
app.MapGet("/", () => "A lightweight .NET IndieAuth server").RequireAuthorization();

app.Run();
