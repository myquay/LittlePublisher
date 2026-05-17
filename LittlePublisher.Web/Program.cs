using System.Text;
using AspNet.Security.IndieAuth;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var config = builder.Configuration.GetSection("App").Get<AppConfiguration>()
    ?? throw new InvalidOperationException("App configuration not found");
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
});

const string externalMicropubTokenScheme = "ExternalMicropubToken";

if (config.ExternalToken.Enabled &&
    string.Equals(config.ExternalToken.Mode, "Jwt", StringComparison.OrdinalIgnoreCase))
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
else if (config.ExternalToken.Enabled &&
    string.Equals(config.ExternalToken.Mode, "Introspection", StringComparison.OrdinalIgnoreCase))
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
    options.AddPolicy("MicropubToken", policy =>
    {
        if (config.ExternalToken.Enabled)
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
builder.Services.AddSingleton<IPublisherStorage, TableStoragePublisherStorage>();
builder.Services.AddSingleton<IContentGenerator, MarkdownContentGenerator>();
builder.Services.AddSingleton<IWebsiteRepository, GitCliWebsiteRepository>();
builder.Services.AddSingleton<IPublishingService, PublishingService>();
builder.Services.AddHttpClient();

// Controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "https://localhost:5001")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });

    options.AddPolicy("Production", policy =>
    {
        policy.WithOrigins(config.Host)
              .AllowAnyHeader()
              .AllowAnyMethod()
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
