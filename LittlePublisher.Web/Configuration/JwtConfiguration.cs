namespace LittlePublisher.Web.Configuration;

/// <summary>
/// JWT configuration settings.
/// </summary>
public class JwtConfiguration
{
    /// <summary>
    /// JWT token issuer.
    /// </summary>
    public string Issuer { get; set; } = default!;

    /// <summary>
    /// JWT token audience.
    /// </summary>
    public string Audience { get; set; } = default!;

    /// <summary>
    /// Secret key for signing JWT tokens (min 32 characters).
    /// </summary>
    public string SecretKey { get; set; } = default!;

    /// <summary>
    /// Token expiry time in minutes.
    /// </summary>
    public int ExpiryMinutes { get; set; } = 60;
}
