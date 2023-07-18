
namespace LittlePublisher.Web.Configuration
{
    /// <summary>
    /// IndieAuth configuration
    /// </summary>
    public class IndieAuthConfiguration
    {
        /// <summary>
        /// ClientId used for IndieAuth authentication.
        /// </summary>
        public string ClientId { get; set; } = default!;

        /// <summary>
        /// GitHub Authentication provider settings
        /// </summary>
        public GitHubConfiguration GitHub { get; set; } = default!;

    }
}
