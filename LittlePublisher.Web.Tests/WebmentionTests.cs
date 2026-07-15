using LittlePublisher.Web.Services.Webmentions;

namespace LittlePublisher.Web.Tests;

public sealed class WebmentionTests
{
    [Fact]
    public void Normalization_preserves_query_and_fragment_but_removes_default_port()
    {
        Assert.Equal("https://example.com/path?q=1#part", WebmentionKeys.NormalizeUrl("HTTPS://EXAMPLE.COM:443/path?q=1#part"));
        Assert.Equal("https://example.com/path?q=1", WebmentionKeys.NormalizeUrl("https://example.com/path?q=1#part", removeFragment: true));
    }

    [Fact]
    public async Task Extractor_requires_exact_target_and_classifies_reply()
    {
        const string html = """
            <article class="h-entry">
              <a class="p-author h-card" href="https://alice.example/"><span class="p-name">Alice</span></a>
              <h1 class="p-name">A reply</h1>
              <div class="e-content"><a class="u-in-reply-to" href="https://example.net/post/?a=1#reply">the post</a><p>Hello from elsewhere.</p></div>
            </article>
            """;
        var fetch = new SafeFetchResult(new("https://alice.example/reply"), new("https://alice.example/reply"), 200, "text/html", html, [], new Dictionary<string, string[]>());
        var result = await new WebmentionHtml().ExtractAsync(fetch, "https://example.net/post/?a=1#reply", CancellationToken.None);
        Assert.Equal("reply", result.DetectedType);
        Assert.Equal("Alice", result.AuthorName);
        Assert.Contains("Hello from elsewhere", result.DisplayContent);
        await Assert.ThrowsAsync<WebmentionWithdrawnException>(() => new WebmentionHtml().ExtractAsync(fetch, "https://example.net/post/?a=2", CancellationToken.None));
    }

    [Fact]
    public async Task Endpoint_discovery_prefers_http_link_and_preserves_query()
    {
        var headers = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase) { ["Link"] = ["</mentions?token=abc>; rel=\"webmention\""] };
        var fetch = new SafeFetchResult(new("https://example.net/post"), new("https://example.net/post"), 200, "text/html", "<link rel=\"webmention\" href=\"/html-endpoint\">", [], headers);
        var result = await new WebmentionHtml().DiscoverAsync(fetch, CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal("https://example.net/mentions?token=abc", result.Endpoint);
        Assert.Equal("http-link", result.Method);
    }

    [Fact]
    public async Task Repeated_deployments_update_one_pending_draft()
    {
        var storage = new MemoryWebmentionStorage();
        var service = new WebmentionDeploymentService(storage);
        await service.IngestAsync(Manifest("sha-1", "sha256:first", "First context"), CancellationToken.None);
        await service.IngestAsync(Manifest("sha-2", "sha256:second", "Settled context"), CancellationToken.None);

        var items = await storage.ListOutgoingAsync(null, 100, CancellationToken.None);
        var item = Assert.Single(items);
        Assert.Equal("sha256:second", item.LatestSourceHash);
        Assert.Equal("Settled context", item.Context);
        Assert.Equal(2, item.RevisionCount);
        Assert.Equal(WebmentionStates.Draft, item.State);
    }

    private static DeploymentManifest Manifest(string sha, string hash, string context) => new(1, "https://michael-mckenna.com/", "myquay/michael-mckenna.com", sha, DateTimeOffset.UtcNow,
        [new("https://michael-mckenna.com/blog/source/", "blog/source/index.html", "blog/content/post/source.md", "Source", hash, true, [new("https://target.example/item", "mention", "target", context)])]);

    private sealed class MemoryWebmentionStorage : IWebmentionStorage
    {
        private readonly Dictionary<string, OutgoingWebmentionRecord> _outgoing = [];
        private readonly Dictionary<string, SitePageRecord> _pages = [];
        private readonly HashSet<string> _deployments = [];
        public Task<OutgoingWebmentionRecord?> GetOutgoingAsync(string id, CancellationToken cancellationToken) => Task.FromResult(_outgoing.GetValueOrDefault(id));
        public Task SaveOutgoingAsync(OutgoingWebmentionRecord record, CancellationToken cancellationToken) { _outgoing[record.Id] = record; return Task.CompletedTask; }
        public Task<IReadOnlyList<OutgoingWebmentionRecord>> ListOutgoingAsync(string? state, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OutgoingWebmentionRecord>>(_outgoing.Values.Where(x => state is null || x.State == state).Take(take).ToArray());
        public Task<SitePageRecord?> GetSitePageAsync(string sourceUrl, CancellationToken cancellationToken) => Task.FromResult(_pages.GetValueOrDefault(WebmentionKeys.NormalizeUrl(sourceUrl)));
        public Task SaveSitePageAsync(SitePageRecord page, CancellationToken cancellationToken) { _pages[WebmentionKeys.NormalizeUrl(page.SourceUrl)] = page; return Task.CompletedTask; }
        public Task<IReadOnlyList<SitePageRecord>> ListSitePagesAsync(int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<SitePageRecord>>(_pages.Values.Take(take).ToArray());
        public Task<bool> DeploymentExistsAsync(string repository, string commitSha, CancellationToken cancellationToken) => Task.FromResult(_deployments.Contains($"{repository}:{commitSha}"));
        public Task SaveDeploymentAsync(string repository, string commitSha, string status, int pageCount, string? error, CancellationToken cancellationToken) { if (status == "processed") _deployments.Add($"{repository}:{commitSha}"); return Task.CompletedTask; }
        public Task<IncomingWebmentionRecord?> GetIncomingAsync(string id, CancellationToken cancellationToken) => Task.FromResult<IncomingWebmentionRecord?>(null);
        public Task<IncomingWebmentionRecord> UpsertIncomingReceiptAsync(string source, string target, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SaveIncomingAsync(IncomingWebmentionRecord record, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<IncomingWebmentionRecord>> ListIncomingAsync(string? state, int take, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<IncomingWebmentionRecord>>([]);
        public Task DeleteIncomingAsync(string id, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SaveAttemptAsync(OutgoingWebmentionAttempt attempt, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<OutgoingWebmentionAttempt>> ListAttemptsAsync(string outgoingId, CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<OutgoingWebmentionAttempt>>([]);
        public Task<bool> IsDomainBlockedAsync(string host, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task BlockDomainAsync(string host, string? reason, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task CheckHealthAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
