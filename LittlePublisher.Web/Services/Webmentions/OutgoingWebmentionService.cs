using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AngleSharp.Html.Parser;
using LittlePublisher.Web.Services.Publishing;

namespace LittlePublisher.Web.Services.Webmentions;

public interface IWebmentionDeploymentService
{
    Task IngestAsync(DeploymentManifest manifest, CancellationToken cancellationToken);
}

public interface IOutgoingWebmentionService
{
    Task QueueSendAsync(string id, CancellationToken cancellationToken);
    Task SendAsync(string id, CancellationToken cancellationToken);
    Task<OutgoingWebmentionRecord> DeclineAsync(string id, CancellationToken cancellationToken);
    Task ScanAsync(string sourceUrl, bool forceResend, CancellationToken cancellationToken);
}

public sealed class WebmentionDeploymentService : IWebmentionDeploymentService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly IWebmentionStorage _storage;

    public WebmentionDeploymentService(IWebmentionStorage storage) => _storage = storage;

    public async Task IngestAsync(DeploymentManifest manifest, CancellationToken cancellationToken)
    {
        if (manifest.SchemaVersion != 1 || string.IsNullOrWhiteSpace(manifest.Repository) || string.IsNullOrWhiteSpace(manifest.CommitSha)) throw new InvalidOperationException("Unsupported or incomplete deployment manifest.");
        if (await _storage.DeploymentExistsAsync(manifest.Repository, manifest.CommitSha, cancellationToken)) return;
        await _storage.SaveDeploymentAsync(manifest.Repository, manifest.CommitSha, "processing", manifest.Pages.Count, null, cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var existingPages = await _storage.ListSitePagesAsync(200, cancellationToken);
            var currentSources = manifest.Pages.Select(x => WebmentionKeys.NormalizeUrl(x.SourceUrl)).ToHashSet(StringComparer.Ordinal);
            foreach (var page in manifest.Pages)
            {
                var priorPage = await _storage.GetSitePageAsync(page.SourceUrl, cancellationToken);
                var priorLinks = DeserializeLinks(priorPage?.OutgoingLinksJson);
                var currentLinks = page.OutgoingLinks
                    .Where(x => Uri.TryCreate(x.TargetUrl, UriKind.Absolute, out var u) && u.Scheme is "http" or "https")
                    .GroupBy(x => WebmentionKeys.NormalizeUrl(x.TargetUrl), StringComparer.Ordinal).Select(x => x.First()).ToArray();
                await _storage.SaveSitePageAsync(new(page.SourceUrl, manifest.CommitSha, page.RepositoryContentPath, page.Title, page.ContentHash, page.AcceptsWebmentions, JsonSerializer.Serialize(currentLinks, Json), priorPage?.FirstSeenUtc ?? now, now, null), cancellationToken);

                foreach (var link in currentLinks) await ReconcilePresentAsync(page, link, manifest.CommitSha, now, cancellationToken);
                var removed = priorLinks.Where(old => currentLinks.All(current => WebmentionKeys.NormalizeUrl(current.TargetUrl) != WebmentionKeys.NormalizeUrl(old.TargetUrl)));
                foreach (var link in removed) await ReconcileRemovedAsync(page, link, manifest.CommitSha, now, cancellationToken);
            }

            foreach (var old in existingPages.Where(x => !currentSources.Contains(WebmentionKeys.NormalizeUrl(x.SourceUrl)) && x.RemovedUtc is null))
                await _storage.SaveSitePageAsync(old with { DeploymentSha = manifest.CommitSha, LastSeenUtc = now, RemovedUtc = now }, cancellationToken);
            await _storage.SaveDeploymentAsync(manifest.Repository, manifest.CommitSha, "processed", manifest.Pages.Count, null, cancellationToken);
        }
        catch (Exception ex)
        {
            await _storage.SaveDeploymentAsync(manifest.Repository, manifest.CommitSha, "failed", manifest.Pages.Count, ex.Message, cancellationToken);
            throw;
        }
    }

    private async Task ReconcilePresentAsync(DeploymentPage page, DeploymentLink link, string sha, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var id = WebmentionKeys.Pair(page.SourceUrl, link.TargetUrl);
        var existing = await _storage.GetOutgoingAsync(id, cancellationToken);
        if (existing?.LastSentSourceHash == page.ContentHash && existing.State == WebmentionStates.Sent) return;
        var intent = existing?.LastSentSourceHash is null ? "new" : "update";
        if (existing is not null && existing.LastSentSourceHash == page.ContentHash)
        {
            await _storage.SaveOutgoingAsync(existing with { State = WebmentionStates.Superseded, Reason = "returned-to-last-sent-version", LatestDeploymentSha = sha, LatestSourceHash = page.ContentHash, LastObservedUtc = now, UpdatedUtc = now }, cancellationToken);
            return;
        }
        var record = existing is null
            ? new OutgoingWebmentionRecord(id, page.SourceUrl, link.TargetUrl, WebmentionStates.Draft, intent, NormalizeRelationship(link.Relationship), page.Title, link.AnchorText, link.Context, page.ContentHash, null, null, sha, null, null, "new-link", 1, now, now, null, null, now)
            : existing with { Source = page.SourceUrl, Target = link.TargetUrl, State = WebmentionStates.Draft, Intent = intent, Relationship = NormalizeRelationship(link.Relationship), SourceTitle = page.Title, AnchorText = link.AnchorText, Context = link.Context, LatestSourceHash = page.ContentHash, ApprovedSourceHash = null, LatestDeploymentSha = sha, Reason = existing.Intent == "withdrawal" ? "target-reappeared" : "source-updated", RevisionCount = existing.RevisionCount + 1, LastObservedUtc = now, FailureReason = null, UpdatedUtc = now };
        await _storage.SaveOutgoingAsync(record, cancellationToken);
    }

    private async Task ReconcileRemovedAsync(DeploymentPage page, DeploymentLink link, string sha, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var id = WebmentionKeys.Pair(page.SourceUrl, link.TargetUrl);
        var existing = await _storage.GetOutgoingAsync(id, cancellationToken);
        if (existing is null) return;
        var next = existing.LastSentSourceHash is null
            ? existing with { State = WebmentionStates.Superseded, Reason = "unsent-target-removed", ApprovedSourceHash = null, LatestDeploymentSha = sha, LatestSourceHash = page.ContentHash, RevisionCount = existing.RevisionCount + 1, LastObservedUtc = now, UpdatedUtc = now }
            : existing with { State = WebmentionStates.WithdrawalDraft, Intent = "withdrawal", Reason = "target-removed", ApprovedSourceHash = null, LatestDeploymentSha = sha, LatestSourceHash = page.ContentHash, RevisionCount = existing.RevisionCount + 1, LastObservedUtc = now, UpdatedUtc = now };
        await _storage.SaveOutgoingAsync(next, cancellationToken);
    }

    private static DeploymentLink[] DeserializeLinks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<DeploymentLink[]>(json, Json) ?? []; } catch (JsonException) { return []; }
    }
    private static string NormalizeRelationship(string? value) => value is "like" or "reply" ? value : "mention";
}

public sealed class OutgoingWebmentionService : IOutgoingWebmentionService
{
    private static readonly JsonSerializerOptions RepositoryJson = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly IWebmentionStorage _storage;
    private readonly IWebmentionQueue _queue;
    private readonly ISafeWebFetcher _fetcher;
    private readonly IWebmentionExtractor _extractor;
    private readonly IWebmentionEndpointDiscovery _discovery;
    private readonly IWebmentionDeploymentService _deployments;
    private readonly IWebsiteRepository _repository;

    public OutgoingWebmentionService(IWebmentionStorage storage, IWebmentionQueue queue, ISafeWebFetcher fetcher, IWebmentionExtractor extractor, IWebmentionEndpointDiscovery discovery, IWebmentionDeploymentService deployments, IWebsiteRepository repository)
    { _storage = storage; _queue = queue; _fetcher = fetcher; _extractor = extractor; _discovery = discovery; _deployments = deployments; _repository = repository; }

    public async Task QueueSendAsync(string id, CancellationToken cancellationToken)
    {
        var record = await _storage.GetOutgoingAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Outgoing Webmention was not found.");
        if (record.State is not (WebmentionStates.Draft or WebmentionStates.WithdrawalDraft or WebmentionStates.Failed or WebmentionStates.NoEndpoint)) throw new InvalidOperationException("This Webmention is not ready to send.");
        await _storage.SaveOutgoingAsync(record with { State = WebmentionStates.QueuedToSend, ApprovedSourceHash = record.LatestSourceHash, FailureReason = null, UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
        await _queue.EnqueueSendAsync(id, cancellationToken);
    }

    public async Task SendAsync(string id, CancellationToken cancellationToken)
    {
        var record = await _storage.GetOutgoingAsync(id, cancellationToken);
        if (record is null) return;
        if (record.State == WebmentionStates.SentActivityPending)
        {
            await PublishActivityAsync(record, cancellationToken);
            return;
        }
        if (record.State != WebmentionStates.QueuedToSend) return;
        if (record.ApprovedSourceHash != record.LatestSourceHash)
        {
            await _storage.SaveOutgoingAsync(record with { State = record.Intent == "withdrawal" ? WebmentionStates.WithdrawalDraft : WebmentionStates.Draft, ApprovedSourceHash = null, FailureReason = "Changed since approval.", UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
            return;
        }
        var attemptNumber = (await _storage.ListAttemptsAsync(id, cancellationToken)).Count + 1;
        var started = DateTimeOffset.UtcNow;
        await _storage.SaveOutgoingAsync(record with { State = WebmentionStates.Sending, UpdatedUtc = started }, cancellationToken);
        string? endpoint = null;
        try
        {
            var source = await _fetcher.FetchAsync(new Uri(record.Source), cancellationToken);
            if (source.StatusCode is < 200 or >= 300) throw new SafeFetchException($"Source returned HTTP {source.StatusCode}.", source.StatusCode >= 500);
            if (record.Intent != "withdrawal") await _extractor.ExtractAsync(source, record.Target, cancellationToken);
            var target = await _fetcher.FetchAsync(new Uri(record.Target), cancellationToken);
            var discovered = await _discovery.DiscoverAsync(target, cancellationToken);
            if (discovered is null)
            {
                await _storage.SaveOutgoingAsync(record with { State = WebmentionStates.NoEndpoint, ApprovedSourceHash = null, FailureReason = "The target does not advertise a Webmention endpoint.", UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
                return;
            }
            endpoint = discovered.Endpoint;
            var response = await _fetcher.PostFormAsync(new Uri(endpoint), new Dictionary<string, string> { ["source"] = record.Source, ["target"] = record.Target }, cancellationToken);
            var success = response.StatusCode is >= 200 and < 300;
            var retryable = response.StatusCode is 429 or >= 500;
            var excerpt = response.Body.Length <= 500 ? response.Body : response.Body[..500];
            var location = response.Headers.TryGetValue("Location", out var values) ? values.FirstOrDefault() : null;
            await _storage.SaveAttemptAsync(new(Guid.NewGuid().ToString("N"), id, attemptNumber, endpoint, started, DateTimeOffset.UtcNow, response.StatusCode, location, excerpt, success ? null : "http", retryable), cancellationToken);
            if (!success)
            {
                await _storage.SaveOutgoingAsync(record with { State = WebmentionStates.Failed, Endpoint = endpoint, DiscoveryMethod = discovered.Method, ApprovedSourceHash = null, FailureReason = $"Endpoint returned HTTP {response.StatusCode}.", UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
                return;
            }
            var sent = DateTimeOffset.UtcNow;
            var delivered = record with { State = WebmentionStates.SentActivityPending, Endpoint = endpoint, DiscoveryMethod = discovered.Method, LastSentSourceHash = record.LatestSourceHash, LastSentUtc = sent, ApprovedSourceHash = null, FailureReason = null, UpdatedUtc = sent };
            await _storage.SaveOutgoingAsync(delivered, cancellationToken);
            await PublishActivityAsync(delivered, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or SafeFetchException or WebmentionWithdrawnException)
        {
            var retryable = ex is HttpRequestException or TaskCanceledException || ex is SafeFetchException { Retryable: true };
            await _storage.SaveAttemptAsync(new(Guid.NewGuid().ToString("N"), id, attemptNumber, endpoint, started, DateTimeOffset.UtcNow, null, null, null, ex.GetType().Name, retryable), cancellationToken);
            await _storage.SaveOutgoingAsync(record with { State = WebmentionStates.Failed, Endpoint = endpoint, ApprovedSourceHash = null, FailureReason = ex.Message, UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
        }
    }

    private async Task PublishActivityAsync(OutgoingWebmentionRecord record, CancellationToken cancellationToken)
    {
        var sent = record.LastSentUtc ?? DateTimeOffset.UtcNow;
        var path = $"blog/data/webmentions/outgoing/{WebmentionKeys.Hash(WebmentionKeys.NormalizeUrl(record.Source))}/{WebmentionKeys.Hash(WebmentionKeys.NormalizeUrl(record.Target))}.json";
        var activity = new OutgoingActivityProjection(1, record.Id, record.Source, record.Target, record.Relationship, record.SourceTitle, null, sent);
        await _repository.MutateFilesAsync([RepositoryFileMutation.Upsert(path, JsonSerializer.Serialize(activity, RepositoryJson) + "\n")], $"Record sent Webmention to {new Uri(record.Target).IdnHost}", cancellationToken);
        await _storage.SaveOutgoingAsync(record with { State = WebmentionStates.Sent, FailureReason = null, UpdatedUtc = DateTimeOffset.UtcNow }, cancellationToken);
    }

    public async Task<OutgoingWebmentionRecord> DeclineAsync(string id, CancellationToken cancellationToken)
    {
        var record = await _storage.GetOutgoingAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Outgoing Webmention was not found.");
        var declined = record with { State = WebmentionStates.Declined, ApprovedSourceHash = null, UpdatedUtc = DateTimeOffset.UtcNow };
        await _storage.SaveOutgoingAsync(declined, cancellationToken);
        return declined;
    }

    public async Task ScanAsync(string sourceUrl, bool forceResend, CancellationToken cancellationToken)
    {
        var registered = await _storage.GetSitePageAsync(sourceUrl, cancellationToken) ?? throw new InvalidOperationException("The source is not in the deployed-page registry.");
        var fetch = await _fetcher.FetchAsync(new Uri(sourceUrl), cancellationToken);
        if (fetch.StatusCode is < 200 or >= 300) throw new InvalidOperationException($"Source returned HTTP {fetch.StatusCode}.");
        var document = await new HtmlParser().ParseDocumentAsync(fetch.Body, cancellationToken);
        var content = document.QuerySelector(".h-entry .e-content") ?? throw new InvalidOperationException("Source does not contain an h-entry e-content region.");
        var links = content.QuerySelectorAll("a[href]").Select(a => new { Element = a, Url = Resolve(fetch.FinalUri, a.GetAttribute("href")) })
            .Where(x => x.Url is not null && x.Url.Scheme is "http" or "https" && x.Url.IdnHost != fetch.FinalUri.IdnHost)
            .GroupBy(x => WebmentionKeys.NormalizeUrl(x.Url!.AbsoluteUri)).Select(g => g.First())
            .Select(x => new DeploymentLink(x.Url!.AbsoluteUri, x.Element.ClassList.Contains("u-like-of") ? "like" : x.Element.ClassList.Contains("u-in-reply-to") ? "reply" : "mention", x.Element.TextContent.Trim(), null)).ToArray();
        var hash = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content.TextContent))).ToLowerInvariant();
        var manifest = new DeploymentManifest(1, new Uri(sourceUrl).GetLeftPart(UriPartial.Authority), "manual-scan", $"manual-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}", DateTimeOffset.UtcNow,
            [new(sourceUrl, string.Empty, registered.RepositoryContentPath, registered.Title, forceResend ? hash + ":force" : hash, registered.AcceptsWebmentions, links)]);
        await _deployments.IngestAsync(manifest, cancellationToken);
    }

    private static Uri? Resolve(Uri baseUri, string? value) => Uri.TryCreate(baseUri, value, out var uri) ? uri : null;
}
