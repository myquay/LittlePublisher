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

public record NewPublishJob(
    string UserMe,
    string? ClientId,
    string Action,
    string RequestJson);
