namespace LittlePublisher.Web.Services.Publishing;

public record ImportRepositoryRequest(
    bool Overwrite = false,
    bool DryRun = false);

public record ImportRepositoryResult(
    int Scanned,
    int Imported,
    int Skipped,
    int Failed,
    IReadOnlyList<ImportRepositoryError> Errors);

public record ImportRepositoryError(
    string FilePath,
    string Message);
