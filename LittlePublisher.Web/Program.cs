using IndieAuth;
using IndieAuth.Authentication;
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
                .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
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
                    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    options.ClientId = config.IndieAuth.ClientId;
                    options.CallbackPath = "/authentication/indie-auth/callback";
                });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(CookieAuthenticationDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddRazorPages().AddRazorPagesOptions(options =>
{
    options.Conventions.AddPageRoute("/Account/SignIn", "/account/sign-in");
});

builder.Services.AddSingleton(new HttpClient());

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
app.MapGet("/", () => "A MicroPub endpoint").RequireAuthorization();

app.Run();
