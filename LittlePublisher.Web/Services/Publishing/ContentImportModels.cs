namespace LittlePublisher.Web.Services.Publishing;

public record ImportRepositoryRequest(
    bool Overwrite = false,
    bool DryRun = false);

public record ImportRepositoryResult(
    int Scanned,
    int Imported,
    int Skipped,
    int Failed,
    IReadOnlyList<ImportRepositoryError> Errors,
    int Ambiguous = 0,
    int DraftsInRepository = 0,
    IReadOnlyList<string>? DraftFiles = null);

public record ImportRepositoryError(
    string FilePath,
    string Message);
