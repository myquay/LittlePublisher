using IndieAuth;
using IndieAuth.Authentication;
using LittlePublisher.Web.Configuration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Linq.Expressions;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration.GetSection("App").Get<AppConfiguration>();

var authBuilder = builder.Services.AddAuthentication()
                .AddCookie(IndieAuthDefaults.ExternalCookieSignInScheme, options =>
                {
                    options.LoginPath = "/account/sign-in";
                    options.Events.OnRedirectToLogin = context =>
                    {
                        if (context.Properties.Items.TryGetValue("me", out string? me) && me != null)
                            context.RedirectUri = QueryHelpers.AddQueryString(context.RedirectUri, "me", me);

                        if (context.Properties.Items.TryGetValue("client_id", out string? clientId) && clientId != null)
                            context.RedirectUri = QueryHelpers.AddQueryString(context.RedirectUri, "client_id", clientId);

                        context.Response.Redirect(context.RedirectUri);
                        return Task.CompletedTask;
                    };
                })
                .AddCookie(IndieAuthDefaults.SignInScheme, options =>
                {
                    options.ForwardChallenge = IndieAuthDefaults.ExternalCookieSignInScheme;
                })
                .AddIndieAuth(IndieAuthDefaults.AuthenticationScheme, options =>
                {
                    options.CallbackPath = "/indie-auth/complete";
                    options.Issuer = config.Host;
                    options.Scopes = new[] { "profile", "create", "update", "delete", "media" };

                    options.AuthorizationEndpoint = "/indie-auth/authorization";
                    options.TokenEndpoint = "/indie-auth/token";
                    options.IntrospectionEndpoint = "/indie-auth/token-info";

                    options.SignInScheme = IndieAuthDefaults.SignInScheme;

                });

if (config.IndieAuth.GitHub.Enabled)
{
    authBuilder.AddGitHub(IndieAuthMethod.GITHUB, options =>
    {
        options.SignInScheme = IndieAuthDefaults.ExternalCookieSignInScheme;
        options.ClientId = config.IndieAuth.GitHub.ClientId;
        options.ClientSecret = config.IndieAuth.GitHub.ClientSecret;
        options.CallbackPath = "/indie-auth/process/github";
    });
}

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(IndieAuthDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRazorPages().AddRazorPagesOptions(options =>
{
    options.Conventions.AddPageRoute("/Account/SignIn", "/account/sign-in");
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.MapGet("/", () => "Hello World!")
    .RequireAuthorization();

app.Run();
