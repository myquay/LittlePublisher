namespace LittlePublisher.Web.Services.Publishing;

public interface IWebsiteRepository
{
    Task<string> PublishFileAsync(string relativePath, string content, string commitMessage, CancellationToken cancellationToken);

    Task CheckConnectionAsync(CancellationToken cancellationToken);
}
