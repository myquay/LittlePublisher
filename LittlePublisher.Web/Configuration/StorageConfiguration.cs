namespace LittlePublisher.Web.Configuration;

/// <summary>
/// Configuration for durable storage.
/// </summary>
public class StorageConfiguration
{
    /// <summary>
    /// Azure Table Storage connection string.
    /// </summary>
    public string ConnectionString { get; set; } = default!;

    /// <summary>
    /// Private blob container for staged photos.
    /// </summary>
    public string MediaContainer { get; set; } = "littlepublisher-staged-media";

    /// <summary>Prefix for LittlePublisher tables.</summary>
    public string TablePrefix { get; set; } = "LittlePublisher";
}
