namespace LittlePublisher.Web.Services.Publishing;

public interface IContentImportService
{
    Task<ImportRepositoryResult> ImportRepositoryAsync(ImportRepositoryRequest request, CancellationToken cancellationToken);
}
