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
                .AddCookie(IndieAuthDefaults.ExternalCookieSignInScheme, options =>
                {
                    options.LoginPath = "/account/sign-in";
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

//TODO: ADD ALL SUPPORTED METHODS OF SIGNING IN

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(IndieAuthDefaults.AuthenticationScheme)
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
