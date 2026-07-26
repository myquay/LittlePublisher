namespace LittlePublisher.Web.Services.Publishing;

public interface IPublishingService
{
    Task<PublishCreateResult> PublishCreateAsync(PublishCreateRequest request, CancellationToken cancellationToken);

    Task<string> DeleteAsync(string relativePath, string commitMessage, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This publishing implementation does not support deletion.");
}
