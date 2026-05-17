using System.Security.Cryptography;
using System.Text;
using Azure;
using Azure.Data.Tables;
using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Services.Storage;

public class TableStoragePublisherStorage : IPublisherStorage
{
    private readonly TableClient? _jobs;
    private readonly TableClient? _items;
    private readonly SemaphoreSlim _initializeLock = new(1, 1);
    private bool _initialized;

    public TableStoragePublisherStorage(AppConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(config.Storage.ConnectionString))
        {
            return;
        }

        var prefix = NormalizeTablePrefix(config.Storage.TablePrefix);
        _jobs = new TableClient(config.Storage.ConnectionString, $"{prefix}PublishJobs");
        _items = new TableClient(config.Storage.ConnectionString, $"{prefix}PublishedItems");
    }

    public async Task<PublishJobRecord> CreatePublishJobAsync(NewPublishJob job, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var entity = new PublishJobEntity
        {
            PartitionKey = "job",
            RowKey = Guid.NewGuid().ToString("N"),
            UserMe = job.UserMe,
            ClientId = job.ClientId,
            Action = job.Action,
            Status = "running",
            RequestJson = job.RequestJson,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        await Jobs.AddEntityAsync(entity, cancellationToken);

        return entity.ToRecord();
    }

    public async Task<PublishJobRecord> CompletePublishJobAsync(string jobId, string publishedUrl, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);

        var entity = await GetPublishJobEntityAsync(jobId, cancellationToken);
        entity.Status = "succeeded";
        entity.PublishedUrl = publishedUrl;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;

        await Jobs.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);

        return entity.ToRecord();
    }

    public async Task<PublishJobRecord> FailPublishJobAsync(string jobId, string error, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);

        var entity = await GetPublishJobEntityAsync(jobId, cancellationToken);
        entity.Status = "failed";
        entity.Error = error;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;

        await Jobs.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);

        return entity.ToRecord();
    }

    public async Task SavePublishedItemAsync(NewPublishedItem item, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);

        var entity = new PublishedItemEntity
        {
            PartitionKey = "item",
            RowKey = StableRowKey(item.Url),
            Url = item.Url,
            Title = item.Title,
            Content = item.Content,
            CategoriesJson = System.Text.Json.JsonSerializer.Serialize(item.Categories),
            PublishedUtc = item.PublishedUtc,
            FilePath = item.FilePath,
            CommitSha = item.CommitSha,
            PropertiesJson = item.PropertiesJson
        };

        await Items.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken);
    }

    public async Task<PublishedItemRecord?> GetPublishedItemByUrlAsync(string url, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);

        var response = await Items.GetEntityIfExistsAsync<PublishedItemEntity>("item", StableRowKey(url), cancellationToken: cancellationToken);

        return response.HasValue ? response.Value!.ToRecord() : null;
    }

    public async Task<IReadOnlyList<PublishJobRecord>> GetRecentPublishJobsAsync(int take, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);

        var entities = new List<PublishJobEntity>();
        await foreach (var entity in Jobs.QueryAsync<PublishJobEntity>(
            filter: "PartitionKey eq 'job'",
            cancellationToken: cancellationToken))
        {
            entities.Add(entity);
        }

        return entities
            .OrderByDescending(entity => entity.CreatedUtc)
            .Take(NormalizeTake(take))
            .Select(entity => entity.ToRecord())
            .ToArray();
    }

    public async Task<IReadOnlyList<PublishedItemRecord>> GetRecentPublishedItemsAsync(int take, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);

        var entities = new List<PublishedItemEntity>();
        await foreach (var entity in Items.QueryAsync<PublishedItemEntity>(
            filter: "PartitionKey eq 'item'",
            cancellationToken: cancellationToken))
        {
            entities.Add(entity);
        }

        return entities
            .OrderByDescending(entity => entity.PublishedUtc)
            .Take(NormalizeTake(take))
            .Select(entity => entity.ToRecord())
            .ToArray();
    }

    public async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);

        await foreach (var _ in Jobs.QueryAsync<PublishJobEntity>(
            filter: "PartitionKey eq 'job'",
            maxPerPage: 1,
            cancellationToken: cancellationToken))
        {
            break;
        }
    }

    private async Task EnsureTablesAsync(CancellationToken cancellationToken)
    {
        if (_initialized)
        {
            return;
        }

        await _initializeLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized)
            {
                return;
            }

            await Jobs.CreateIfNotExistsAsync(cancellationToken);
            await Items.CreateIfNotExistsAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initializeLock.Release();
        }
    }

    private async Task<PublishJobEntity> GetPublishJobEntityAsync(string jobId, CancellationToken cancellationToken)
    {
        var response = await Jobs.GetEntityIfExistsAsync<PublishJobEntity>("job", jobId, cancellationToken: cancellationToken);

        if (!response.HasValue)
        {
            throw new InvalidOperationException($"Publish job '{jobId}' was not found.");
        }

        return response.Value!;
    }

    private TableClient Jobs => _jobs ?? throw new InvalidOperationException("App:Storage:ConnectionString is not configured.");

    private TableClient Items => _items ?? throw new InvalidOperationException("App:Storage:ConnectionString is not configured.");

    private static string NormalizeTablePrefix(string? prefix)
    {
        var normalized = new string((prefix ?? "LittlePublisher").Where(char.IsLetterOrDigit).ToArray());

        if (normalized.Length < 3)
        {
            normalized = "LittlePublisher";
        }

        return normalized.Length <= 40 ? normalized : normalized[..40];
    }

    private static string StableRowKey(string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static int NormalizeTake(int take)
    {
        return Math.Clamp(take, 1, 50);
    }

    private sealed class PublishJobEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;

        public string RowKey { get; set; } = default!;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        public string UserMe { get; set; } = default!;

        public string? ClientId { get; set; }

        public string Action { get; set; } = default!;

        public string Status { get; set; } = default!;

        public string? PublishedUrl { get; set; }

        public string? Error { get; set; }

        public string RequestJson { get; set; } = default!;

        public DateTimeOffset CreatedUtc { get; set; }

        public DateTimeOffset UpdatedUtc { get; set; }

        public PublishJobRecord ToRecord()
        {
            return new PublishJobRecord(
                Id: RowKey,
                UserMe: UserMe,
                ClientId: ClientId,
                Action: Action,
                Status: Status,
                PublishedUrl: PublishedUrl,
                Error: Error,
                RequestJson: RequestJson,
                CreatedUtc: CreatedUtc,
                UpdatedUtc: UpdatedUtc);
        }
    }

    private sealed class PublishedItemEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;

        public string RowKey { get; set; } = default!;

        public DateTimeOffset? Timestamp { get; set; }

        public ETag ETag { get; set; }

        public string Url { get; set; } = default!;

        public string? Title { get; set; }

        public string Content { get; set; } = default!;

        public string CategoriesJson { get; set; } = "[]";

        public DateTimeOffset PublishedUtc { get; set; }

        public string? FilePath { get; set; }

        public string? CommitSha { get; set; }

        public string PropertiesJson { get; set; } = default!;

        public PublishedItemRecord ToRecord()
        {
            var categories = System.Text.Json.JsonSerializer.Deserialize<IReadOnlyList<string>>(CategoriesJson) ?? Array.Empty<string>();

            return new PublishedItemRecord(
                Id: RowKey,
                Url: Url,
                Title: Title,
                Content: Content,
                Categories: categories,
                PublishedUtc: PublishedUtc,
                FilePath: FilePath,
                CommitSha: CommitSha,
                PropertiesJson: PropertiesJson);
        }
    }
}
