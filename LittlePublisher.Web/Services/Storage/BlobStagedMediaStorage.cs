using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Services.Storage;

// Staging must survive restarts and must never be served by the public website.
public sealed class BlobStagedMediaStorage(AppConfiguration config) : IStagedMediaStorage
{
    private BlobContainerClient Container => string.IsNullOrWhiteSpace(config.Storage.ConnectionString)
        ? throw new InvalidOperationException("Configure durable storage before uploading media.")
        : new BlobContainerClient(config.Storage.ConnectionString, config.Storage.MediaContainer);

    public async Task PutAsync(string id, StagedMedia media, CancellationToken cancellationToken)
    {
        var container = Container;
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        var properties = await container.GetPropertiesAsync(cancellationToken: cancellationToken);
        if (properties.Value.PublicAccess != PublicAccessType.None)
            throw new InvalidOperationException("The media staging container must be private.");
        await container.GetBlobClient(id).UploadAsync(BinaryData.FromBytes(media.Content), new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = media.ContentType },
            Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }
        }, cancellationToken);
    }

    public async Task<StagedMedia?> GetAsync(string id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await Container.GetBlobClient(id).DownloadContentAsync(cancellationToken);
            return new StagedMedia(result.Value.Content.ToArray(), result.Value.Details.ContentType);
        }
        catch (RequestFailedException ex) when (ex.Status == 404) { return null; }
    }
}
