namespace LittlePublisher.Web.Configuration
{
    /// <summary>
    /// Little Publisher's App configuration.
    /// </summary>
    public class AppConfiguration
    {
        /// <summary>
        /// Host for this LittlePublisher instance.
        /// </summary>
        public string Host { get; set; } = default!;

        /// <summary>
        /// Gets or sets the website that LittlePublisher will publish to.
        /// </summary>
        public WebsiteConfiguration Website { get; set; } = new();

        /// <summary>
        /// Gets or sets the GitHub checkout and publishing configuration.
        /// </summary>
        public GitHubConfiguration GitHub { get; set; } = new();

        /// <summary>
        /// Gets or sets durable storage configuration.
        /// </summary>
        public StorageConfiguration Storage { get; set; } = new();

        /// <summary>
        /// Gets or sets the IndieAuth configuration.
        /// </summary>
        public IndieAuthConfiguration IndieAuth { get; set; } = new();

        /// <summary>
        /// Gets or sets the JWT configuration.
        /// </summary>
        public JwtConfiguration Jwt { get; set; } = new();
    }
}
