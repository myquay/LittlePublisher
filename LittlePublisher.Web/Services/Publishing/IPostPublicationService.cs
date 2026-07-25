using LittlePublisher.Web.Services.Storage;

namespace LittlePublisher.Web.Services.Publishing;

public interface IPostPublicationService
{
    Task<PostRecord> PublishAsync(string postId, CancellationToken cancellationToken);
}
