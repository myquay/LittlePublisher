namespace LittlePublisher.Web.Services.Storage;

public static class PostStates
{
    public const string Draft = "draft";
    public const string Publishing = "publishing";
    public const string Published = "published";
    public const string PublishFailed = "publish-failed";
    public const string Deleted = "deleted";
}

public record PostRecord(
    string Id,
    string? Title,
    string Content,
    string? Summary,
    IReadOnlyList<string> Categories,
    string Slug,
    string PostType,
    string State,
    int WorkingRevision,
    int? PublishedRevision,
    DateTimeOffset? RequestedPublishedUtc,
    DateTimeOffset? PublishedUtc,
    string? PublishedUrl,
    string? FilePath,
    string? CommitSha,
    string? LastPublishError,
    string? SourceRepositoryPath,
    string? SourceCommitSha,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,
    string ETag,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Properties = null,
    DateTimeOffset? DeletedUtc = null)
{
    public bool HasUnpublishedChanges => PublishedRevision is null || WorkingRevision > PublishedRevision;

    public IReadOnlyDictionary<string, IReadOnlyList<string>> MicropubProperties =>
        Properties ?? EmptyProperties;

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> EmptyProperties =
        new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
}

public record NewPost(
    string? Title,
    string Content,
    string? Summary,
    IReadOnlyList<string> Categories,
    string Slug,
    string PostType,
    DateTimeOffset? RequestedPublishedUtc = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Properties = null);

public record PostUpdate(
    string? Title,
    string Content,
    string? Summary,
    IReadOnlyList<string> Categories,
    string Slug,
    string PostType,
    DateTimeOffset? RequestedPublishedUtc = null,
    IReadOnlyDictionary<string, IReadOnlyList<string>>? Properties = null);

public record ImportedPost(
    string? Title,
    string Content,
    string? Summary,
    IReadOnlyList<string> Categories,
    string Slug,
    string PostType,
    bool Draft,
    DateTimeOffset? PublishedUtc,
    string PublishedUrl,
    string RepositoryPath,
    string CommitSha);
