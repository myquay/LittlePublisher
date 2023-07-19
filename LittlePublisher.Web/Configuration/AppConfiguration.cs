namespace LittlePublisher.Web.Configuration
{
    /// <summary>
    /// Little Publisher's App configuration
    /// </summary>
    public class AppConfiguration
    {
        /// <summary>
        /// Host for website
        /// </summary>
        public string Host { get; set; } = default!;

        /// <summary>
        /// Gets or sets the IndieAuth configuration.
        /// </summary>
        public IndieAuthConfiguration IndieAuth { get; set; } = default!;
    }
}
