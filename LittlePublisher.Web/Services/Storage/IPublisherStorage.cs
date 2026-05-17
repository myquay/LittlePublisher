namespace LittlePublisher.Web.Services.Storage;

public interface IPublisherStorage
{
    Task<PublishJobRecord> CreatePublishJobAsync(NewPublishJob job, CancellationToken cancellationToken);

    Task<PublishJobRecord> CompletePublishJobAsync(string jobId, string publishedUrl, CancellationToken cancellationToken);

    Task<PublishJobRecord> FailPublishJobAsync(string jobId, string error, CancellationToken cancellationToken);

    Task SavePublishedItemAsync(NewPublishedItem item, CancellationToken cancellationToken);

    Task<PublishedItemRecord?> GetPublishedItemByUrlAsync(string url, CancellationToken cancellationToken);
}
