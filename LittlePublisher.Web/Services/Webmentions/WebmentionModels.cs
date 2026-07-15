using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace LittlePublisher.Web.Services.Webmentions;

public static class WebmentionStates
{
    public const string Queued = "queued";
    public const string Verifying = "verifying";
    public const string PendingReview = "pending-review";
    public const string UpdatePendingReview = "update-pending-review";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Invalid = "invalid";
    public const string TransientFailure = "transient-failure";
    public const string Withdrawn = "withdrawn";
    public const string Draft = "draft";
    public const string WithdrawalDraft = "withdrawal-draft";
    public const string Declined = "declined";
    public const string QueuedToSend = "queued";
    public const string Sending = "sending";
    public const string SentActivityPending = "sent-activity-pending";
    public const string Sent = "sent";
    public const string Failed = "failed";
    public const string Superseded = "superseded";
    public const string NoEndpoint = "no-endpoint";
}

public static class WebmentionKeys
{
    public static string NormalizeUrl(string value, bool removeFragment = false)
    {
        var uri = new Uri(value, UriKind.Absolute);
        var builder = new UriBuilder(uri)
        {
            Scheme = uri.Scheme.ToLowerInvariant(),
            Host = uri.IdnHost.ToLowerInvariant(),
            Port = uri.IsDefaultPort ? -1 : uri.Port,
            Path = string.IsNullOrEmpty(uri.AbsolutePath) ? "/" : uri.AbsolutePath,
            Fragment = removeFragment ? string.Empty : uri.Fragment
        };
        return builder.Uri.AbsoluteUri;
    }

    public static string Hash(params string[] values)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', values)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string Pair(string source, string target) => Hash(NormalizeUrl(source), NormalizeUrl(target));
}

public sealed record IncomingWebmentionRecord(
    string Id,
    string Source,
    string Target,
    string State,
    string? DetectedType,
    string? PresentationType,
    string? AuthorName,
    string? AuthorUrl,
    string? Title,
    string? DisplayContent,
    DateTimeOffset? SourcePublishedUtc,
    string? ContentHash,
    string? PreviousProjectionJson,
    string? VerificationEvidence,
    string? FailureReason,
    int FailureCount,
    string? ModerationReason,
    string? RepositoryPath,
    string? RepositoryCommitSha,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    DateTimeOffset? VerifiedUtc,
    DateTimeOffset? ApprovedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record OutgoingWebmentionRecord(
    string Id,
    string Source,
    string Target,
    string State,
    string Intent,
    string Relationship,
    string? SourceTitle,
    string? AnchorText,
    string? Context,
    string LatestSourceHash,
    string? ApprovedSourceHash,
    string? LastSentSourceHash,
    string LatestDeploymentSha,
    string? Endpoint,
    string? DiscoveryMethod,
    string Reason,
    int RevisionCount,
    DateTimeOffset FirstDiscoveredUtc,
    DateTimeOffset LastObservedUtc,
    DateTimeOffset? LastSentUtc,
    string? FailureReason,
    DateTimeOffset UpdatedUtc);

public sealed record OutgoingWebmentionAttempt(
    string Id,
    string OutgoingId,
    int AttemptNumber,
    string? Endpoint,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    int? HttpStatus,
    string? ResponseLocation,
    string? ResponseExcerpt,
    string? FailureCategory,
    bool Retryable);

public sealed record SitePageRecord(
    string SourceUrl,
    string DeploymentSha,
    string? RepositoryContentPath,
    string? Title,
    string ContentHash,
    bool AcceptsWebmentions,
    string OutgoingLinksJson,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    DateTimeOffset? RemovedUtc);

public sealed record DeploymentManifest(
    int SchemaVersion,
    string Site,
    string Repository,
    string CommitSha,
    DateTimeOffset GeneratedUtc,
    IReadOnlyList<DeploymentPage> Pages);

public sealed record DeploymentPage(
    string SourceUrl,
    string SourcePath,
    string? RepositoryContentPath,
    string? Title,
    string ContentHash,
    bool AcceptsWebmentions,
    IReadOnlyList<DeploymentLink> OutgoingLinks);

public sealed record DeploymentLink(
    string TargetUrl,
    string Relationship,
    string? AnchorText,
    string? Context);

public sealed record WebmentionQueueMessage(int SchemaVersion, string Operation, string RecordId);

public sealed record SafeFetchResult(
    Uri RequestedUri,
    Uri FinalUri,
    int StatusCode,
    string ContentType,
    string Body,
    IReadOnlyList<string> Redirects,
    IReadOnlyDictionary<string, string[]> Headers);

public sealed record ExtractedWebmention(
    string DetectedType,
    string PresentationType,
    string? AuthorName,
    string? AuthorUrl,
    string? Title,
    string DisplayContent,
    DateTimeOffset? PublishedUtc,
    string ContentHash,
    string Evidence);

public sealed record EndpointDiscoveryResult(string Endpoint, string Method);

public sealed record RepositoryWebmentionProjection(
    int SchemaVersion,
    string Id,
    string Source,
    string Target,
    string Type,
    RepositoryAuthorProjection? Author,
    string DisplayContent,
    DateTimeOffset? SourcePublishedUtc,
    DateTimeOffset ReceivedUtc,
    DateTimeOffset VerifiedUtc,
    DateTimeOffset ApprovedUtc);

public sealed record RepositoryAuthorProjection(string Name, string? Url);

public sealed record OutgoingActivityProjection(
    int SchemaVersion,
    string Id,
    string Source,
    string Target,
    string Type,
    string? SourceTitle,
    DateTimeOffset? SourcePublishedUtc,
    DateTimeOffset SentUtc);

public sealed record WebmentionListResponse<T>(IReadOnlyList<T> Items);

public sealed record RejectWebmentionRequest(string? Reason, bool BlockDomain = false);
public sealed record ScanPageRequest(string SourceUrl, bool ForceResend = false);
public sealed record CreateLikeRequest(string TargetUrl, string? Comment);
public sealed record CreateReplyRequest(string TargetUrl, string Comment, string? TargetTitle);
public sealed record CreateBlogrollRequest(string Name, string SiteUrl, string? FeedUrl, string? Comment);
