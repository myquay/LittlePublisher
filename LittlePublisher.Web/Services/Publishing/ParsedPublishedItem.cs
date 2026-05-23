namespace LittlePublisher.Web.Services.Publishing;

public record ParsedPublishedItem(
    string Url,
    string? Title,
    string Content,
    string? Summary,
    IReadOnlyList<string> Categories,
    DateTimeOffset PublishedUtc,
    string FilePath,
    string? CommitSha);
