namespace LittlePublisher.Web.Configuration;

/// <summary>
/// Configuration for the website LittlePublisher edits.
/// </summary>
public class WebsiteConfiguration
{
    /// <summary>
    /// Public website URL that published entries belong to.
    /// </summary>
    public string Url { get; set; } = default!;

    /// <summary>
    /// Default author display name for generated content.
    /// </summary>
    public string? AuthorName { get; set; }

    /// <summary>
    /// Default author photo URL for generated content.
    /// </summary>
    public string? AuthorPhoto { get; set; }
}
