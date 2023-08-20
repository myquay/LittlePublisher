namespace LittlePublisher.Web.Infrastructure
{
    /// <summary>
    /// Response from GitHub API
    /// </summary>
    public class GithubUserModel
    {
        /// <summary>
        /// User entered into GitHub
        /// </summary>
        public string Blog { get; set; } = default!;
    }
}
