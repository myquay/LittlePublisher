using System.Text.Json;
using Azure;
using Azure.Data.Tables;
using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Services.Webmentions;

public sealed class TableStorageWebmentionStorage : IWebmentionStorage
{
    private readonly TableClient? _incoming;
    private readonly TableClient? _outgoing;
    private readonly TableClient? _attempts;
    private readonly TableClient? _pages;
    private readonly TableClient? _deployments;
    private readonly TableClient? _domainRules;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _initialized;

    public TableStorageWebmentionStorage(AppConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.Storage.ConnectionString)) return;
        var prefix = NormalizePrefix(config.Storage.TablePrefix);
        _incoming = NewTable(config, $"{prefix}IncomingWebmentions");
        _outgoing = NewTable(config, $"{prefix}OutgoingWebmentions");
        _attempts = NewTable(config, $"{prefix}WebmentionAttempts");
        _pages = NewTable(config, $"{prefix}WebmentionSitePages");
        _deployments = NewTable(config, $"{prefix}WebmentionDeployments");
        _domainRules = NewTable(config, $"{prefix}WebmentionDomainRules");
    }

    public async Task<IncomingWebmentionRecord?> GetIncomingAsync(string id, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var result = await Incoming.GetEntityIfExistsAsync<TableEntity>("incoming", id, cancellationToken: cancellationToken);
        return result.HasValue ? ReadIncoming(result.Value!) : null;
    }

    public async Task<IncomingWebmentionRecord> UpsertIncomingReceiptAsync(string source, string target, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var id = WebmentionKeys.Pair(source, target);
        var existing = await GetIncomingAsync(id, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var record = existing is null
            ? new IncomingWebmentionRecord(id, source, target, WebmentionStates.Queued, null, null, null, null, null, null, null, null, null, null, null, 0, null, null, null, now, now, null, null, now)
            : existing with { Source = source, Target = target, State = WebmentionStates.Queued, LastSeenUtc = now, FailureReason = null, UpdatedUtc = now };
        await SaveIncomingAsync(record, cancellationToken);
        return record;
    }

    public async Task SaveIncomingAsync(IncomingWebmentionRecord record, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        await Incoming.UpsertEntityAsync(WriteIncoming(record), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<IReadOnlyList<IncomingWebmentionRecord>> ListIncomingAsync(string? state, int take, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var records = new List<IncomingWebmentionRecord>();
        var filter = string.IsNullOrWhiteSpace(state) ? "PartitionKey eq 'incoming'" : $"PartitionKey eq 'incoming' and State eq '{Escape(state)}'";
        await foreach (var entity in Incoming.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken)) records.Add(ReadIncoming(entity));
        return records.OrderByDescending(x => x.UpdatedUtc).Take(NormalizeTake(take)).ToArray();
    }

    public async Task DeleteIncomingAsync(string id, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        await Incoming.DeleteEntityAsync("incoming", id, ETag.All, cancellationToken);
    }

    public async Task<OutgoingWebmentionRecord?> GetOutgoingAsync(string id, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var result = await Outgoing.GetEntityIfExistsAsync<TableEntity>("outgoing", id, cancellationToken: cancellationToken);
        return result.HasValue ? ReadOutgoing(result.Value!) : null;
    }

    public async Task SaveOutgoingAsync(OutgoingWebmentionRecord record, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        await Outgoing.UpsertEntityAsync(WriteOutgoing(record), TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<IReadOnlyList<OutgoingWebmentionRecord>> ListOutgoingAsync(string? state, int take, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var records = new List<OutgoingWebmentionRecord>();
        var filter = string.IsNullOrWhiteSpace(state) ? "PartitionKey eq 'outgoing'" : $"PartitionKey eq 'outgoing' and State eq '{Escape(state)}'";
        await foreach (var entity in Outgoing.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken)) records.Add(ReadOutgoing(entity));
        return records.OrderByDescending(x => x.UpdatedUtc).Take(NormalizeTake(take)).ToArray();
    }

    public async Task SaveAttemptAsync(OutgoingWebmentionAttempt attempt, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var entity = new TableEntity(attempt.OutgoingId, attempt.Id)
        {
            ["AttemptNumber"] = attempt.AttemptNumber, ["Endpoint"] = attempt.Endpoint,
            ["StartedUtc"] = attempt.StartedUtc, ["CompletedUtc"] = attempt.CompletedUtc,
            ["HttpStatus"] = attempt.HttpStatus, ["ResponseLocation"] = attempt.ResponseLocation,
            ["ResponseExcerpt"] = attempt.ResponseExcerpt, ["FailureCategory"] = attempt.FailureCategory,
            ["Retryable"] = attempt.Retryable
        };
        await Attempts.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<IReadOnlyList<OutgoingWebmentionAttempt>> ListAttemptsAsync(string outgoingId, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var records = new List<OutgoingWebmentionAttempt>();
        await foreach (var e in Attempts.QueryAsync<TableEntity>($"PartitionKey eq '{Escape(outgoingId)}'", cancellationToken: cancellationToken))
        {
            records.Add(new OutgoingWebmentionAttempt(e.RowKey, e.PartitionKey, GetInt(e, "AttemptNumber"), GetString(e, "Endpoint"), GetDate(e, "StartedUtc"), GetDate(e, "CompletedUtc"), GetNullableInt(e, "HttpStatus"), GetString(e, "ResponseLocation"), GetString(e, "ResponseExcerpt"), GetString(e, "FailureCategory"), GetBool(e, "Retryable")));
        }
        return records.OrderByDescending(x => x.AttemptNumber).ToArray();
    }

    public async Task<SitePageRecord?> GetSitePageAsync(string sourceUrl, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var id = WebmentionKeys.Hash(WebmentionKeys.NormalizeUrl(sourceUrl));
        var result = await Pages.GetEntityIfExistsAsync<TableEntity>("page", id, cancellationToken: cancellationToken);
        return result.HasValue ? ReadPage(result.Value!) : null;
    }

    public async Task SaveSitePageAsync(SitePageRecord page, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var e = new TableEntity("page", WebmentionKeys.Hash(WebmentionKeys.NormalizeUrl(page.SourceUrl)))
        {
            ["SourceUrl"] = page.SourceUrl, ["DeploymentSha"] = page.DeploymentSha,
            ["RepositoryContentPath"] = page.RepositoryContentPath, ["Title"] = page.Title,
            ["ContentHash"] = page.ContentHash, ["AcceptsWebmentions"] = page.AcceptsWebmentions,
            ["OutgoingLinksJson"] = page.OutgoingLinksJson, ["FirstSeenUtc"] = page.FirstSeenUtc,
            ["LastSeenUtc"] = page.LastSeenUtc, ["RemovedUtc"] = page.RemovedUtc
        };
        await Pages.UpsertEntityAsync(e, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<IReadOnlyList<SitePageRecord>> ListSitePagesAsync(int take, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var records = new List<SitePageRecord>();
        await foreach (var entity in Pages.QueryAsync<TableEntity>("PartitionKey eq 'page'", cancellationToken: cancellationToken)) records.Add(ReadPage(entity));
        return records.OrderByDescending(x => x.LastSeenUtc).Take(NormalizeTake(take)).ToArray();
    }

    public async Task<bool> DeploymentExistsAsync(string repository, string commitSha, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var result = await Deployments.GetEntityIfExistsAsync<TableEntity>(WebmentionKeys.Hash(repository), commitSha, cancellationToken: cancellationToken);
        return result.HasValue && string.Equals(GetString(result.Value!, "Status"), "processed", StringComparison.Ordinal);
    }

    public async Task SaveDeploymentAsync(string repository, string commitSha, string status, int pageCount, string? error, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var e = new TableEntity(WebmentionKeys.Hash(repository), commitSha)
        {
            ["Repository"] = repository, ["Status"] = status, ["PageCount"] = pageCount,
            ["Error"] = error, ["ReceivedUtc"] = DateTimeOffset.UtcNow
        };
        await Deployments.UpsertEntityAsync(e, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<bool> IsDomainBlockedAsync(string host, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        return (await DomainRules.GetEntityIfExistsAsync<TableEntity>("blocked", host.ToLowerInvariant(), cancellationToken: cancellationToken)).HasValue;
    }

    public async Task BlockDomainAsync(string host, string? reason, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var e = new TableEntity("blocked", host.ToLowerInvariant()) { ["Reason"] = reason, ["CreatedUtc"] = DateTimeOffset.UtcNow };
        await DomainRules.UpsertEntityAsync(e, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        await foreach (var _ in Incoming.QueryAsync<TableEntity>("PartitionKey eq 'incoming'", maxPerPage: 1, cancellationToken: cancellationToken)) break;
    }

    private async Task EnsureTablesAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;
        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            foreach (var table in new[] { Incoming, Outgoing, Attempts, Pages, Deployments, DomainRules }) await table.CreateIfNotExistsAsync(cancellationToken);
            _initialized = true;
        }
        finally { _initializeLock.Release(); }
    }

    private static TableClient NewTable(AppConfiguration config, string name) => new(config.Storage.ConnectionString, name);
    private TableClient Incoming => _incoming ?? Missing();
    private TableClient Outgoing => _outgoing ?? Missing();
    private TableClient Attempts => _attempts ?? Missing();
    private TableClient Pages => _pages ?? Missing();
    private TableClient Deployments => _deployments ?? Missing();
    private TableClient DomainRules => _domainRules ?? Missing();
    private static TableClient Missing() => throw new InvalidOperationException("App:Storage:ConnectionString is not configured.");

    private static TableEntity WriteIncoming(IncomingWebmentionRecord r) => new("incoming", r.Id)
    {
        ["Source"] = r.Source, ["Target"] = r.Target, ["State"] = r.State,
        ["DetectedType"] = r.DetectedType, ["PresentationType"] = r.PresentationType,
        ["AuthorName"] = r.AuthorName, ["AuthorUrl"] = r.AuthorUrl, ["Title"] = r.Title,
        ["DisplayContent"] = r.DisplayContent, ["SourcePublishedUtc"] = r.SourcePublishedUtc,
        ["ContentHash"] = r.ContentHash, ["PreviousProjectionJson"] = r.PreviousProjectionJson,
        ["VerificationEvidence"] = r.VerificationEvidence, ["FailureReason"] = r.FailureReason,
        ["FailureCount"] = r.FailureCount, ["ModerationReason"] = r.ModerationReason,
        ["RepositoryPath"] = r.RepositoryPath, ["RepositoryCommitSha"] = r.RepositoryCommitSha,
        ["FirstSeenUtc"] = r.FirstSeenUtc, ["LastSeenUtc"] = r.LastSeenUtc,
        ["VerifiedUtc"] = r.VerifiedUtc, ["ApprovedUtc"] = r.ApprovedUtc, ["UpdatedUtc"] = r.UpdatedUtc
    };

    private static IncomingWebmentionRecord ReadIncoming(TableEntity e) => new(e.RowKey, Required(e, "Source"), Required(e, "Target"), Required(e, "State"), GetString(e, "DetectedType"), GetString(e, "PresentationType"), GetString(e, "AuthorName"), GetString(e, "AuthorUrl"), GetString(e, "Title"), GetString(e, "DisplayContent"), GetNullableDate(e, "SourcePublishedUtc"), GetString(e, "ContentHash"), GetString(e, "PreviousProjectionJson"), GetString(e, "VerificationEvidence"), GetString(e, "FailureReason"), GetInt(e, "FailureCount"), GetString(e, "ModerationReason"), GetString(e, "RepositoryPath"), GetString(e, "RepositoryCommitSha"), GetDate(e, "FirstSeenUtc"), GetDate(e, "LastSeenUtc"), GetNullableDate(e, "VerifiedUtc"), GetNullableDate(e, "ApprovedUtc"), GetDate(e, "UpdatedUtc"));

    private static TableEntity WriteOutgoing(OutgoingWebmentionRecord r) => new("outgoing", r.Id)
    {
        ["Source"] = r.Source, ["Target"] = r.Target, ["State"] = r.State, ["Intent"] = r.Intent,
        ["Relationship"] = r.Relationship, ["SourceTitle"] = r.SourceTitle, ["AnchorText"] = r.AnchorText,
        ["Context"] = r.Context, ["LatestSourceHash"] = r.LatestSourceHash,
        ["ApprovedSourceHash"] = r.ApprovedSourceHash,
        ["LastSentSourceHash"] = r.LastSentSourceHash, ["LatestDeploymentSha"] = r.LatestDeploymentSha,
        ["Endpoint"] = r.Endpoint, ["DiscoveryMethod"] = r.DiscoveryMethod, ["Reason"] = r.Reason,
        ["RevisionCount"] = r.RevisionCount, ["FirstDiscoveredUtc"] = r.FirstDiscoveredUtc,
        ["LastObservedUtc"] = r.LastObservedUtc, ["LastSentUtc"] = r.LastSentUtc,
        ["FailureReason"] = r.FailureReason, ["UpdatedUtc"] = r.UpdatedUtc
    };

    private static OutgoingWebmentionRecord ReadOutgoing(TableEntity e) => new(e.RowKey, Required(e, "Source"), Required(e, "Target"), Required(e, "State"), Required(e, "Intent"), Required(e, "Relationship"), GetString(e, "SourceTitle"), GetString(e, "AnchorText"), GetString(e, "Context"), Required(e, "LatestSourceHash"), GetString(e, "ApprovedSourceHash"), GetString(e, "LastSentSourceHash"), Required(e, "LatestDeploymentSha"), GetString(e, "Endpoint"), GetString(e, "DiscoveryMethod"), Required(e, "Reason"), GetInt(e, "RevisionCount"), GetDate(e, "FirstDiscoveredUtc"), GetDate(e, "LastObservedUtc"), GetNullableDate(e, "LastSentUtc"), GetString(e, "FailureReason"), GetDate(e, "UpdatedUtc"));
    private static SitePageRecord ReadPage(TableEntity e) => new(Required(e, "SourceUrl"), Required(e, "DeploymentSha"), GetString(e, "RepositoryContentPath"), GetString(e, "Title"), Required(e, "ContentHash"), GetBool(e, "AcceptsWebmentions"), Required(e, "OutgoingLinksJson"), GetDate(e, "FirstSeenUtc"), GetDate(e, "LastSeenUtc"), GetNullableDate(e, "RemovedUtc"));

    private static string Required(TableEntity e, string key) => GetString(e, key) ?? throw new InvalidOperationException($"Webmention table property '{key}' is missing.");
    private static string? GetString(TableEntity e, string key) => e.TryGetValue(key, out var value) ? value as string : null;
    private static int GetInt(TableEntity e, string key) => e.TryGetValue(key, out var value) && value is int number ? number : 0;
    private static int? GetNullableInt(TableEntity e, string key) => e.TryGetValue(key, out var value) ? value as int? : null;
    private static bool GetBool(TableEntity e, string key) => e.TryGetValue(key, out var value) && value is bool flag && flag;
    private static DateTimeOffset GetDate(TableEntity e, string key) => GetNullableDate(e, key) ?? DateTimeOffset.MinValue;
    private static DateTimeOffset? GetNullableDate(TableEntity e, string key) => e.TryGetValue(key, out var value) ? value as DateTimeOffset? : null;
    private static string Escape(string value) => value.Replace("'", "''", StringComparison.Ordinal);
    private static int NormalizeTake(int take) => Math.Clamp(take, 1, 200);
    private static string NormalizePrefix(string? prefix)
    {
        var value = new string((prefix ?? "LittlePublisher").Where(char.IsLetterOrDigit).ToArray());
        if (value.Length < 3) value = "LittlePublisher";
        return value.Length <= 35 ? value : value[..35];
    }
}
