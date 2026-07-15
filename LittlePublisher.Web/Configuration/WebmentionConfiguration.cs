namespace LittlePublisher.Web.Configuration;

public sealed class WebmentionConfiguration
{
    public bool Enabled { get; set; }
    public string PublicEndpoint { get; set; } = string.Empty;
    public string[] OwnedOrigins { get; set; } = [];
    public string DeploymentWebhookSecret { get; set; } = string.Empty;
    public string QueuePrefix { get; set; } = "littlepublisher";
    public int FetchTimeoutSeconds { get; set; } = 5;
    public int MaxRedirects { get; set; } = 5;
    public int MaxResponseBytes { get; set; } = 1_048_576;
    public int[] AllowedPorts { get; set; } = [80, 443];
    public bool AutomaticSend { get; set; }
    public bool AutomaticPublishIncoming { get; set; }
    public int ReverifyApprovedAfterDays { get; set; } = 7;
}
