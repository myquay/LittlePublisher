namespace LittlePublisher.Web.Services.Webmentions;

public interface IWebmentionStorage
{
    Task<IncomingWebmentionRecord?> GetIncomingAsync(string id, CancellationToken cancellationToken);
    Task<IncomingWebmentionRecord> UpsertIncomingReceiptAsync(string source, string target, CancellationToken cancellationToken);
    Task SaveIncomingAsync(IncomingWebmentionRecord record, CancellationToken cancellationToken);
    Task<IReadOnlyList<IncomingWebmentionRecord>> ListIncomingAsync(string? state, int take, CancellationToken cancellationToken);
    Task DeleteIncomingAsync(string id, CancellationToken cancellationToken);

    Task<OutgoingWebmentionRecord?> GetOutgoingAsync(string id, CancellationToken cancellationToken);
    Task SaveOutgoingAsync(OutgoingWebmentionRecord record, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutgoingWebmentionRecord>> ListOutgoingAsync(string? state, int take, CancellationToken cancellationToken);
    Task SaveAttemptAsync(OutgoingWebmentionAttempt attempt, CancellationToken cancellationToken);
    Task<IReadOnlyList<OutgoingWebmentionAttempt>> ListAttemptsAsync(string outgoingId, CancellationToken cancellationToken);

    Task<SitePageRecord?> GetSitePageAsync(string sourceUrl, CancellationToken cancellationToken);
    Task SaveSitePageAsync(SitePageRecord page, CancellationToken cancellationToken);
    Task<IReadOnlyList<SitePageRecord>> ListSitePagesAsync(int take, CancellationToken cancellationToken);
    Task<bool> DeploymentExistsAsync(string repository, string commitSha, CancellationToken cancellationToken);
    Task SaveDeploymentAsync(string repository, string commitSha, string status, int pageCount, string? error, CancellationToken cancellationToken);
    Task<bool> IsDomainBlockedAsync(string host, CancellationToken cancellationToken);
    Task BlockDomainAsync(string host, string? reason, CancellationToken cancellationToken);
    Task CheckHealthAsync(CancellationToken cancellationToken);
}
