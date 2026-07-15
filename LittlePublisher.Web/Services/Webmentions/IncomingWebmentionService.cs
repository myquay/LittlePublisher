using System.Text.Json;
using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Publishing;

namespace LittlePublisher.Web.Services.Webmentions;

public interface IIncomingWebmentionService
{
    Task<IncomingWebmentionRecord> ReceiveAsync(string source, string target, CancellationToken cancellationToken);
    Task VerifyAsync(string id, CancellationToken cancellationToken);
    Task<IncomingWebmentionRecord> ApproveAsync(string id, CancellationToken cancellationToken);
    Task<IncomingWebmentionRecord> RejectAsync(string id, string? reason, bool blockDomain, CancellationToken cancellationToken);
    Task ReverifyAsync(string id, CancellationToken cancellationToken);
    Task WithdrawAsync(IncomingWebmentionRecord record, string reason, CancellationToken cancellationToken);
}

public sealed class IncomingWebmentionService : IIncomingWebmentionService
{
    private static readonly JsonSerializerOptions RepositoryJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly AppConfiguration _config;
    private readonly IWebmentionStorage _storage;
    private readonly IWebmentionQueue _queue;
    private readonly ISafeWebFetcher _fetcher;
    private readonly IWebmentionExtractor _extractor;
    private readonly IWebsiteRepository _repository;

    public IncomingWebmentionService(AppConfiguration config, IWebmentionStorage storage, IWebmentionQueue queue, ISafeWebFetcher fetcher, IWebmentionExtractor extractor, IWebsiteRepository repository)
    {
        _config = config; _storage = storage; _queue = queue; _fetcher = fetcher; _extractor = extractor; _repository = repository;
    }

    public async Task<IncomingWebmentionRecord> ReceiveAsync(string source, string target, CancellationToken cancellationToken)
    {
        if (!_config.Webmention.Enabled) throw new WebmentionDisabledException();
        var sourceUri = ParseProtocolUrl(source, nameof(source));
        var targetUri = ParseProtocolUrl(target, nameof(target));
        if (source.Length > 2048 || target.Length > 2048) throw new WebmentionRequestException("source and target must be no longer than 2048 characters.");
        if (WebmentionKeys.NormalizeUrl(source) == WebmentionKeys.NormalizeUrl(target)) throw new WebmentionRequestException("source and target must be different URLs.");
        if (!IsOwnedTarget(targetUri)) throw new WebmentionRequestException("target is not an eligible owned URL.");
        if (await _storage.IsDomainBlockedAsync(sourceUri.IdnHost, cancellationToken)) throw new WebmentionBlockedException();
        var targetPageUrl = WebmentionKeys.NormalizeUrl(target, removeFragment: true);
        var page = await _storage.GetSitePageAsync(targetPageUrl, cancellationToken);
        if (page is null)
        {
            try
            {
                var targetFetch = await _fetcher.FetchAsync(new Uri(targetPageUrl), cancellationToken);
                if (targetFetch.StatusCode is < 200 or >= 300 || !targetFetch.Body.Contains("rel=\"webmention\"", StringComparison.OrdinalIgnoreCase))
                    throw new WebmentionRequestException("target is not registered as an eligible page.");
                var now = DateTimeOffset.UtcNow;
                page = new(targetPageUrl, "receiver-race", null, null, $"sha256:{WebmentionKeys.Hash(targetFetch.Body)}", true, "[]", now, now, null);
                await _storage.SaveSitePageAsync(page, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SafeFetchException)
            {
                throw new WebmentionRequestException("target is not registered as an eligible page.");
            }
        }
        if (!page.AcceptsWebmentions || page.RemovedUtc is not null) throw new WebmentionRequestException("target is not registered as an eligible page.");
        var record = await _storage.UpsertIncomingReceiptAsync(source, target, cancellationToken);
        await _queue.EnqueueVerificationAsync(record.Id, cancellationToken);
        return record;
    }

    public async Task VerifyAsync(string id, CancellationToken cancellationToken)
    {
        var record = await _storage.GetIncomingAsync(id, cancellationToken);
        if (record is null) return;
        if (record.State == WebmentionStates.Rejected) return;
        await _storage.SaveIncomingAsync(record with { State = WebmentionStates.Verifying, UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
        try
        {
            var fetch = await _fetcher.FetchAsync(new Uri(record.Source), cancellationToken);
            var extracted = await _extractor.ExtractAsync(fetch, record.Target, cancellationToken);
            var changedApproved = record.ApprovedUtc is not null && !string.Equals(record.ContentHash, extracted.ContentHash, StringComparison.Ordinal);
            var unchangedApproved = record.ApprovedUtc is not null && !changedApproved;
            var previous = changedApproved ? JsonSerializer.Serialize(new { record.AuthorName, record.AuthorUrl, record.Title, record.DisplayContent, record.DetectedType, record.ContentHash }) : record.PreviousProjectionJson;
            var next = record with
            {
                State = unchangedApproved ? WebmentionStates.Approved : changedApproved ? WebmentionStates.UpdatePendingReview : WebmentionStates.PendingReview,
                DetectedType = extracted.DetectedType, PresentationType = extracted.PresentationType,
                AuthorName = extracted.AuthorName, AuthorUrl = extracted.AuthorUrl, Title = extracted.Title,
                DisplayContent = extracted.DisplayContent, SourcePublishedUtc = extracted.PublishedUtc,
                ContentHash = extracted.ContentHash, PreviousProjectionJson = previous,
                VerificationEvidence = extracted.Evidence, FailureReason = null, FailureCount = 0,
                VerifiedUtc = DateTimeOffset.UtcNow, LastSeenUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow
            };
            await _storage.SaveIncomingAsync(next, cancellationToken);
        }
        catch (WebmentionWithdrawnException ex)
        {
            if (record.ApprovedUtc is not null) await WithdrawAsync(record, ex.Message, cancellationToken);
            else await _storage.SaveIncomingAsync(record with { State = WebmentionStates.Invalid, FailureReason = ex.Message, VerifiedUtc = DateTimeOffset.UtcNow, UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SafeFetchException)
        {
            var retryable = ex is not SafeFetchException safe || safe.Retryable;
            if (record.ApprovedUtc is not null && record.FailureCount >= 2 && ex.Message.Contains("404", StringComparison.Ordinal))
            {
                await WithdrawAsync(record, "Source remained 404 Not Found beyond the verification grace period.", cancellationToken);
                return;
            }
            await _storage.SaveIncomingAsync(record with { State = retryable ? WebmentionStates.TransientFailure : WebmentionStates.Invalid, FailureReason = ex.Message, FailureCount = record.FailureCount + 1, UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
            if (retryable && record.FailureCount < 4) throw;
        }
    }

    public async Task<IncomingWebmentionRecord> ApproveAsync(string id, CancellationToken cancellationToken)
    {
        var record = await _storage.GetIncomingAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Incoming Webmention was not found.");
        if (record.State is not (WebmentionStates.PendingReview or WebmentionStates.UpdatePendingReview)) throw new InvalidOperationException("Only a verified pending Webmention can be approved.");
        if (record.VerifiedUtc is null || record.PresentationType is null || record.DisplayContent is null) throw new InvalidOperationException("The Webmention does not have a verified public projection.");
        var now = DateTimeOffset.UtcNow;
        var path = IncomingRepositoryPath(record.Target, record.Source);
        var projection = new RepositoryWebmentionProjection(1, record.Id, record.Source, record.Target, record.PresentationType,
            string.IsNullOrWhiteSpace(record.AuthorName) ? null : new(record.AuthorName, record.AuthorUrl), record.DisplayContent,
            record.SourcePublishedUtc, record.FirstSeenUtc, record.VerifiedUtc.Value, now);
        var content = JsonSerializer.Serialize(projection, RepositoryJson) + "\n";
        var host = new Uri(record.Source).IdnHost;
        var result = await _repository.MutateFilesAsync([RepositoryFileMutation.Upsert(path, content)], $"Approve Webmention from {host}", cancellationToken);
        var approved = record with { State = WebmentionStates.Approved, ApprovedUtc = now, RepositoryPath = path, RepositoryCommitSha = result.CommitSha, PreviousProjectionJson = null, ModerationReason = null, UpdatedUtc = now };
        await _storage.SaveIncomingAsync(approved, cancellationToken);
        return approved;
    }

    public async Task<IncomingWebmentionRecord> RejectAsync(string id, string? reason, bool blockDomain, CancellationToken cancellationToken)
    {
        var record = await _storage.GetIncomingAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Incoming Webmention was not found.");
        if (blockDomain) await _storage.BlockDomainAsync(new Uri(record.Source).IdnHost, reason, cancellationToken);
        var rejected = record with { State = WebmentionStates.Rejected, ModerationReason = reason, UpdatedUtc = DateTimeOffset.UtcNow };
        await _storage.SaveIncomingAsync(rejected, cancellationToken);
        return rejected;
    }

    public async Task ReverifyAsync(string id, CancellationToken cancellationToken)
    {
        var record = await _storage.GetIncomingAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Incoming Webmention was not found.");
        await _storage.SaveIncomingAsync(record with { State = WebmentionStates.Queued, FailureReason = null, UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
        await _queue.EnqueueVerificationAsync(id, cancellationToken);
    }

    public async Task WithdrawAsync(IncomingWebmentionRecord record, string reason, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(record.RepositoryPath))
            await _repository.MutateFilesAsync([RepositoryFileMutation.Delete(record.RepositoryPath)], $"Withdraw Webmention from {new Uri(record.Source).IdnHost}", cancellationToken);
        await _storage.SaveIncomingAsync(record with { State = WebmentionStates.Withdrawn, FailureReason = reason, RepositoryPath = null, UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
    }

    private bool IsOwnedTarget(Uri target) => _config.Webmention.OwnedOrigins.Any(origin => Uri.TryCreate(origin, UriKind.Absolute, out var owned) && string.Equals(owned.Scheme, target.Scheme, StringComparison.OrdinalIgnoreCase) && string.Equals(owned.IdnHost, target.IdnHost, StringComparison.OrdinalIgnoreCase) && owned.Port == target.Port);
    private static Uri ParseProtocolUrl(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) throw new WebmentionRequestException($"{name} must be an absolute HTTP(S) URL without credentials.");
        return uri;
    }
    internal static string IncomingRepositoryPath(string target, string source) => $"blog/data/webmentions/incoming/{WebmentionKeys.Hash(WebmentionKeys.NormalizeUrl(target, true))}/{WebmentionKeys.Hash(WebmentionKeys.NormalizeUrl(source))}.json";
}

public sealed class WebmentionRequestException(string message) : Exception(message);
public sealed class WebmentionDisabledException : Exception;
public sealed class WebmentionBlockedException : Exception;
