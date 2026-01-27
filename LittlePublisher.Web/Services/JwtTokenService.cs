using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using LittlePublisher.Web.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace LittlePublisher.Web.Services;

/// <summary>
/// Service for generating JWT tokens.
/// </summary>
public interface IJwtTokenService
{
    /// <summary>
    /// Generates a JWT token for the authenticated user.
    /// </summary>
    /// <param name="me">The user's IndieAuth "me" URL.</param>
    /// <param name="additionalClaims">Optional additional claims to include.</param>
    /// <returns>The generated JWT token string.</returns>
    string GenerateToken(string me, IEnumerable<Claim>? additionalClaims = null);
}

/// <summary>
/// Implementation of JWT token generation service.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtConfiguration _config;

    public JwtTokenService(AppConfiguration config)
    {
        _config = config.Jwt;
    }

    public string GenerateToken(string me, IEnumerable<Claim>? additionalClaims = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, me),
            new("me", me),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (additionalClaims != null)
        {
            claims.AddRange(additionalClaims);
        }

        var token = new JwtSecurityToken(
            issuer: _config.Issuer,
            audience: _config.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_config.ExpiryMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
