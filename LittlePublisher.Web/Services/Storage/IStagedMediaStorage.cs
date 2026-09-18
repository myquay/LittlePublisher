namespace LittlePublisher.Web.Services.Storage;

public sealed record StagedMedia(byte[] Content, string ContentType);

public interface IStagedMediaStorage
{
    Task PutAsync(string id, StagedMedia media, CancellationToken cancellationToken);
    Task<StagedMedia?> GetAsync(string id, CancellationToken cancellationToken);
}
