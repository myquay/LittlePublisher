namespace LittlePublisher.Web.Configuration;

/// <summary>
/// External Micropub access token validation settings.
/// </summary>
public class ExternalTokenConfiguration
{
    /// <summary>
    /// Enables accepting access tokens from a configured external IndieAuth server.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Token validation mode. Supported values: Jwt, Introspection.
    /// </summary>
    public string Mode { get; set; } = "Introspection";

    /// <summary>
    /// JWT issuer for external access tokens when Mode is Jwt.
    /// </summary>
    public string Issuer { get; set; } = default!;

    /// <summary>
    /// JWT audience for external access tokens when Mode is Jwt.
    /// </summary>
    public string Audience { get; set; } = default!;

    /// <summary>
    /// JWT signing key for external access tokens when Mode is Jwt.
    /// </summary>
    public string SecretKey { get; set; } = default!;

    /// <summary>
    /// Token introspection endpoint when Mode is Introspection.
    /// </summary>
    public string IntrospectionEndpoint { get; set; } = default!;

    /// <summary>
    /// Bearer token used to authenticate this resource server to the introspection endpoint.
    /// </summary>
    public string IntrospectionToken { get; set; } = default!;

    /// <summary>
    /// Requires HTTPS for the introspection endpoint.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;
}
