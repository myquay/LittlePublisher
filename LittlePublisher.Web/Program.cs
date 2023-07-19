using IndieAuth;
using IndieAuth.Authentication;
using LittlePublisher.Web.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Linq.Expressions;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration.GetSection("App").Get<AppConfiguration>();

var authBuilder = builder.Services.AddAuthentication()
                .AddCookie(IndieAuthDefaults.ExternalCookieSignInScheme)
                .AddCookie(IndieAuthDefaults.SignInScheme, options =>
                {
                    options.LoginPath = "/account/sign-in";
                })
                .AddIndieAuth(IndieAuthDefaults.AuthenticationScheme, options =>
                {
                    options.SignInScheme = IndieAuthDefaults.SignInScheme;
                    options.CallbackPath = "/indie-auth/complete";
                    options.Issuer = config.Host;
                    options.Scopes = new[] { "create", "update", "delete", "media" };

                    options.AuthorizationEndpoint = $"{config.Host}/indie-auth/authorization";
                    options.TokenEndpoint = $"{config.Host}/indie-auth/token";
                    options.IntrospectionEndpoint = $"{config.Host}/indie-auth/token-info";
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
    options.DefaultPolicy = new AuthorizationPolicyBuilder(IndieAuthDefaults.SignInScheme)
        .RequireAuthenticatedUser()
        .Build();
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

app.MapGet("/", () => "Hello World!")
    .RequireAuthorization();

app.Run();
