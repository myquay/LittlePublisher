namespace LittlePublisher.Web.Services.Publishing;

public record PublishCreateRequest(
    string? Name,
    string Content,
    string? Summary,
    IReadOnlyList<string> Categories,
    DateTimeOffset PublishedUtc,
    string Slug,
    string PostType = "",
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Properties = null,
    string? ExistingBookPath = null,
    string? ExistingBookUrl = null);

public record PublishCreateResult(
    string Url,
    string FilePath,
    string CommitSha);
