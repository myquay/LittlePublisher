namespace LittlePublisher.Web.Services.Storage;

public interface IPublisherStorage
{
    Task<PublishJobRecord> CreatePublishJobAsync(NewPublishJob job, CancellationToken cancellationToken);

    Task<PublishJobRecord> CompletePublishJobAsync(string jobId, string publishedUrl, CancellationToken cancellationToken);

    Task<PublishJobRecord> FailPublishJobAsync(string jobId, string error, CancellationToken cancellationToken);

    Task<IReadOnlyList<PublishJobRecord>> GetRecentPublishJobsAsync(int take, CancellationToken cancellationToken);

    Task CheckHealthAsync(CancellationToken cancellationToken);
}
