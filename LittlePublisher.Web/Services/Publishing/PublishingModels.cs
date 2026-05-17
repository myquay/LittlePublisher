namespace LittlePublisher.Web.Services.Publishing;

public record PublishCreateRequest(
    string? Name,
    string Content,
    string? Summary,
    IReadOnlyList<string> Categories,
    DateTimeOffset PublishedUtc,
    string Slug);

public record PublishCreateResult(
    string Url,
    string FilePath,
    string CommitSha);
