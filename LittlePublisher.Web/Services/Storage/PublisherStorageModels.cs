namespace LittlePublisher.Web.Services.Storage;

public record PublishJobRecord(
    string Id,
    string UserMe,
    string? ClientId,
    string Action,
    string Status,
    string? PublishedUrl,
    string? Error,
    string RequestJson,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public record PublishedItemRecord(
    string Id,
    string Url,
    string? Title,
    string Content,
    IReadOnlyList<string> Categories,
    DateTimeOffset PublishedUtc,
    string? FilePath,
    string? CommitSha,
    string PropertiesJson);

public record NewPublishJob(
    string UserMe,
    string? ClientId,
    string Action,
    string RequestJson);

public record NewPublishedItem(
    string Url,
    string? Title,
    string Content,
    IReadOnlyList<string> Categories,
    DateTimeOffset PublishedUtc,
    string? FilePath,
    string? CommitSha,
    string PropertiesJson);
