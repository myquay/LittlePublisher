namespace LittlePublisher.Web.Configuration
{
    /// <summary>
    /// Little Publisher's App configuration
    /// </summary>
    public class AppConfiguration
    {
        /// <summary>
        /// Gets or sets the IndieAuth configuration.
        /// </summary>
        public IndieAuthConfiguration IndieAuth { get; set; } = default!;
    }
}
