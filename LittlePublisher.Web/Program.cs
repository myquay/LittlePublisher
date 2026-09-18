using System.Text;
using AspNet.Security.IndieAuth;
using LittlePublisher.Web.Authentication;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using LittlePublisher.Web.Services.Webmentions;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var config = builder.Configuration.GetSection("App").Get<AppConfiguration>()
    ?? throw new InvalidOperationException("App configuration not found");
if (config.Webmention.Enabled)
{
    if (!Uri.TryCreate(config.Webmention.PublicEndpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps)
        throw new InvalidOperationException("App:Webmention:PublicEndpoint must be an absolute HTTPS URL when Webmentions are enabled.");
    if (config.Webmention.OwnedOrigins.Length == 0 || config.Webmention.OwnedOrigins.Any(x => !Uri.TryCreate(x, UriKind.Absolute, out var origin) || origin.Scheme != Uri.UriSchemeHttps))
        throw new InvalidOperationException("App:Webmention:OwnedOrigins must contain at least one absolute HTTPS origin.");
    if (config.Webmention.DeploymentWebhookSecret.Length < 32)
        throw new InvalidOperationException("App:Webmention:DeploymentWebhookSecret must contain at least 32 characters.");
    if (config.Webmention.AutomaticSend || config.Webmention.AutomaticPublishIncoming)
        throw new InvalidOperationException("Automatic Webmention sending and incoming publication are not supported.");
}
builder.Services.AddSingleton(config);

// Authentication
var authenticationBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    // Cookie is only used as a temporary holder during the IndieAuth remote flow.
    // It is not used for session-based auth; JWTs handle that.
    options.Cookie.Name = "IndieAuth.Correlation";
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.ExpireTimeSpan = TimeSpan.FromMinutes(5);
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = config.Jwt.Issuer,
        ValidAudience = config.Jwt.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.Jwt.SecretKey))
    };
})
.AddIndieAuth(IndieAuthDefaults.AuthenticationScheme, options =>
{
    options.ClientId = config.IndieAuth.ClientId;
    options.CallbackPath = "/api/auth/indie-callback";
    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.Events.OnRemoteFailure = IndieAuthRemoteFailureHandler.HandleAsync;
});

const string externalMicropubTokenScheme = "ExternalMicropubToken";
var externalMicropubTokenConfigured = config.ExternalToken.Enabled && config.ExternalToken.IsSupportedMode;

if (config.ExternalToken.Enabled && config.ExternalToken.IsJwtMode)
{
    authenticationBuilder.AddJwtBearer(externalMicropubTokenScheme, options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = config.ExternalToken.Issuer,
            ValidAudience = config.ExternalToken.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config.ExternalToken.SecretKey)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
}
else if (config.ExternalToken.Enabled && config.ExternalToken.IsIntrospectionMode)
{
    authenticationBuilder.AddIndieAuthBearer(externalMicropubTokenScheme, options =>
    {
        options.IntrospectionEndpoint = config.ExternalToken.IntrospectionEndpoint;
        options.IntrospectionAuthenticationMethod = IntrospectionAuthMethod.Bearer;
        options.IntrospectionToken = config.ExternalToken.IntrospectionToken;
        options.RequireHttpsMetadata = config.ExternalToken.RequireHttpsMetadata;
    });
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("StagedMedia", policy =>
    {
        policy.AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme);
        if (externalMicropubTokenConfigured) policy.AddAuthenticationSchemes(externalMicropubTokenScheme);
        policy.RequireAuthenticatedUser();
        policy.RequireAssertion(context => string.Equals(
            (context.User.FindFirst(AspNet.Security.IndieAuth.Infrastructure.IndieAuthClaims.ME)?.Value ?? context.User.FindFirst("me")?.Value ?? context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value)?.TrimEnd('/'),
            config.Website.Url.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
    });
    options.AddPolicy("MicropubToken", policy =>
    {
        if (externalMicropubTokenConfigured)
        {
            policy.AddAuthenticationSchemes(externalMicropubTokenScheme);
            policy.RequireAuthenticatedUser();
        }
        else
        {
            policy.RequireAssertion(_ => false);
        }
    });
});

// Services
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<TableStoragePublisherStorage>();
builder.Services.AddSingleton<IPublisherStorage>(services => services.GetRequiredService<TableStoragePublisherStorage>());
builder.Services.AddSingleton<IPostStorage>(services => services.GetRequiredService<TableStoragePublisherStorage>());
builder.Services.AddSingleton<IContentGenerator, MarkdownContentGenerator>();
builder.Services.AddSingleton<IWebsiteRepository, GitCliWebsiteRepository>();
builder.Services.AddSingleton<IStagedMediaStorage, BlobStagedMediaStorage>();
builder.Services.AddSingleton<MediaPublicationService>();
builder.Services.AddSingleton<IPublishingService, PublishingService>();
builder.Services.AddSingleton<IPostPublicationService, PostPublicationService>();
builder.Services.AddSingleton<MarkdownPublishedItemParser>();
builder.Services.AddSingleton<IContentImportService, ContentImportService>();
builder.Services.AddHttpClient();
builder.Services.AddSingleton<OpenLibraryService>();
builder.Services.AddHttpClient("OpenLibrary", client =>
{
    client.BaseAddress = new Uri("https://openlibrary.org/");
    client.Timeout = TimeSpan.FromSeconds(15);
    client.MaxResponseContentBufferSize = 2 * 1024 * 1024;
    client.DefaultRequestHeaders.UserAgent.ParseAdd("LittlePublisher/1.0");
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddSingleton<IWebmentionStorage, TableStorageWebmentionStorage>();
builder.Services.AddSingleton<IWebmentionQueue, AzureWebmentionQueue>();
builder.Services.AddSingleton<SafeWebFetcher>();
builder.Services.AddSingleton<ISafeWebFetcher>(services => services.GetRequiredService<SafeWebFetcher>());
builder.Services.AddSingleton<WebmentionHtml>();
builder.Services.AddSingleton<IWebmentionExtractor>(services => services.GetRequiredService<WebmentionHtml>());
builder.Services.AddSingleton<IWebmentionEndpointDiscovery>(services => services.GetRequiredService<WebmentionHtml>());
builder.Services.AddSingleton<IWebmentionDeploymentService, WebmentionDeploymentService>();
builder.Services.AddSingleton<IIncomingWebmentionService, IncomingWebmentionService>();
builder.Services.AddSingleton<IOutgoingWebmentionService, OutgoingWebmentionService>();
builder.Services.AddSingleton<IWebmentionContentService, WebmentionContentService>();
builder.Services.AddHostedService<WebmentionWorker>();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("books", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("webmention", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS
var configuredCorsOrigins = CorsConfiguration.GetAllowedOrigins(config);
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins(
                  configuredCorsOrigins
                      .Concat(["http://localhost:5173", "https://localhost:5001"])
                      .Distinct(StringComparer.OrdinalIgnoreCase)
                      .ToArray())
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Location", "Link", "ETag")
              .AllowCredentials();
    });

    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(configuredCorsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .WithExposedHeaders("Location", "Link", "ETag")
              .AllowCredentials();
    });
});

var app = builder.Build();

// Error handling
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRateLimiter();

// CORS
if (app.Environment.IsDevelopment())
{
    app.UseCors("Development");
}
else
{
    app.UseCors("Production");
}

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// Static files and SPA
app.UseDefaultFiles();
app.UseStaticFiles();

// API routes
app.MapControllers();

// SPA fallback
app.MapFallbackToFile("index.html");

app.Run();
