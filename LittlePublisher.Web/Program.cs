using IndieAuth;
using IndieAuth.Authentication;
using LittlePublisher.Web.Configuration;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

                    //options.TokenEndpoint = "/indie-auth/token";
                    //options.AuthorizationEndpoint = "/indie-auth/authorization";
                    //options.IntrospectionEndpoint = "/api/user/me";
                    //options.IntrospectionAuthMethodsSupported = new[] { IndieAuthMethod.GITHUB };
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
