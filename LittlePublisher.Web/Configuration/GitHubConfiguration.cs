
namespace LittlePublisher.Web.Configuration
{
    /// <summary>
    /// GitHub configuration
    /// </summary>
    public class GitHubConfiguration
    {
        /// <summary>
        /// ClientId used for GitHub authentication.
        /// </summary>
        public string ClientId { get; set; } = default!;

        /// <summary>
        /// Client secret used for GitHub authentication.
        /// </summary>
        public string ClientSecret { get; set; } = default!;

        /// <summary>
        /// Whether GitHub authentication is enabled.
        /// </summary>
        public bool Enabled { get; set; }

    }
}
