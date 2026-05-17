namespace LittlePublisher.Web.Configuration;

/// <summary>
/// Configuration for checking out, modifying, and committing website content.
/// </summary>
public class GitHubConfiguration
{
    /// <summary>
    /// Git remote URL for the website repository.
    /// </summary>
    public string RepositoryUrl { get; set; } = default!;

    /// <summary>
    /// Branch LittlePublisher should publish to.
    /// </summary>
    public string Branch { get; set; } = "main";

    /// <summary>
    /// Username used with token-based GitHub authentication.
    /// </summary>
    public string Username { get; set; } = default!;

    /// <summary>
    /// Token used with token-based GitHub authentication.
    /// </summary>
    public string Token { get; set; } = default!;

    /// <summary>
    /// Repository-relative path where generated content should be written.
    /// </summary>
    public string ContentPath { get; set; } = "content/posts";
}
