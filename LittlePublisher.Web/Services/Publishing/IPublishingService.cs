namespace LittlePublisher.Web.Services.Publishing;

public interface IPublishingService
{
    Task<PublishCreateResult> PublishCreateAsync(PublishCreateRequest request, CancellationToken cancellationToken);
}
