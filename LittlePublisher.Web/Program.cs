using System.Text;
using AspNet.Security.IndieAuth;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Configuration
var config = builder.Configuration.GetSection("App").Get<AppConfiguration>()
    ?? throw new InvalidOperationException("App configuration not found");
builder.Services.AddSingleton(config);

// Authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
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
});

builder.Services.AddAuthorization();

// Services
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
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
