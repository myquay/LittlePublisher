using Azure;
using Azure.Data.Tables;
using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Services.Storage;

public class TableStoragePublisherStorage : IPublisherStorage, IPostStorage
{
    private const string SitePartition = "site";
    private const int ContentChunkCharacters = 24_000;
    private const int MaximumContentCharacters = 1_000_000;
    private readonly TableClient? _jobs;
    private readonly TableClient? _posts;
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
        _posts = new TableClient(config.Storage.ConnectionString, $"{prefix}Posts");
    }

    public async Task<PostRecord> CreatePostAsync(NewPost post, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        ValidateContent(post.Content);

        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid().ToString("N");
        var entity = new PostEntity
        {
            PartitionKey = SitePartition,
            RowKey = PostRowKey(id),
            Id = id,
            Title = post.Title,
            Summary = post.Summary,
            CategoriesJson = System.Text.Json.JsonSerializer.Serialize(post.Categories),
            PropertiesJson = SerializeProperties(post.Properties),
            Slug = post.Slug,
            PostType = post.PostType,
            State = PostStates.Draft,
            WorkingRevision = 1,
            RequestedPublishedUtc = post.RequestedPublishedUtc,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        await SubmitRevisionAsync(entity, post.Content, addPost: true, ETag.All, cancellationToken);
        return (await GetPostAsync(id, cancellationToken))!;
    }

    public async Task<PostRecord> UpdatePostAsync(string postId, PostUpdate update, string expectedETag, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        ValidateContent(update.Content);

        var entity = await GetPostEntityAsync(postId, cancellationToken);
        entity.Title = update.Title;
        entity.Summary = update.Summary;
        entity.CategoriesJson = System.Text.Json.JsonSerializer.Serialize(update.Categories);
        entity.PropertiesJson = SerializeProperties(update.Properties);
        entity.Slug = update.Slug;
        entity.PostType = update.PostType;
        entity.RequestedPublishedUtc = update.RequestedPublishedUtc;
        entity.WorkingRevision++;
        entity.State = PostStates.Draft;
        entity.LastPublishError = null;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;

        await SubmitRevisionAsync(entity, update.Content, addPost: false, new ETag(expectedETag), cancellationToken);
        return (await GetPostAsync(postId, cancellationToken))!;
    }

    public async Task<PostRecord?> GetPostAsync(string postId, CancellationToken cancellationToken)
    {
        await EnsureTablesAsync(cancellationToken);
        var response = await Posts.GetEntityIfExistsAsync<PostEntity>(
            SitePartition,
            PostRowKey(postId),
            cancellationToken: cancellationToken);

        if (!response.HasValue)
        {
            return null;
        }

        var entity = response.Value!;
        var content = await ReadRevisionContentAsync(postId, entity.WorkingRevision, cancellationToken);
        return entity.ToRecord(content);
    }

    public async Task<PostRecord?> GetPostByUrlAsync(string url, CancellationToken cancellationToken)
    {
        var entity = (await QueryPostEntitiesAsync(cancellationToken))
            .FirstOrDefault(candidate => string.Equals(candidate.PublishedUrl, url, StringComparison.OrdinalIgnoreCase));
        return entity is null
            ? null
            : entity.ToRecord(await ReadRevisionContentAsync(entity.Id, entity.WorkingRevision, cancellationToken));
    }

    public async Task<PostRecord?> GetPostByRepositoryPathAsync(string repositoryPath, CancellationToken cancellationToken)
    {
        var entity = (await QueryPostEntitiesAsync(cancellationToken))
            .FirstOrDefault(candidate =>
                string.Equals(candidate.SourceRepositoryPath, repositoryPath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.FilePath, repositoryPath, StringComparison.OrdinalIgnoreCase));
        return entity is null
            ? null
            : entity.ToRecord(await ReadRevisionContentAsync(entity.Id, entity.WorkingRevision, cancellationToken));
    }

    public async Task<IReadOnlyList<PostRecord>> GetRecentPostsAsync(int take, CancellationToken cancellationToken)
    {
        var entities = (await QueryPostEntitiesAsync(cancellationToken))
            .OrderByDescending(entity => entity.UpdatedUtc)
            .Take(NormalizeTake(take))
            .ToArray();
        var records = new List<PostRecord>(entities.Length);

        foreach (var entity in entities)
        {
            records.Add(entity.ToRecord(await ReadRevisionContentAsync(entity.Id, entity.WorkingRevision, cancellationToken)));
        }

        return records;
    }

    public async Task<PostRecord> MarkPublishingAsync(string postId, int revision, string expectedETag, CancellationToken cancellationToken)
    {
        var entity = await GetPostEntityAsync(postId, cancellationToken);
        if (entity.WorkingRevision != revision)
        {
            throw new InvalidOperationException("The post changed before publication started.");
        }

        entity.State = PostStates.Publishing;
        entity.LastPublishError = null;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;
        await Posts.UpdateEntityAsync(entity, new ETag(expectedETag), TableUpdateMode.Replace, cancellationToken);
        return (await GetPostAsync(postId, cancellationToken))!;
    }

    public async Task<PostRecord> MarkPublishedAsync(
        string postId,
        int revision,
        string publishedUrl,
        string filePath,
        string commitSha,
        DateTimeOffset publishedUtc,
        CancellationToken cancellationToken)
    {
        var entity = await GetPostEntityAsync(postId, cancellationToken);
        entity.State = PostStates.Published;
        entity.PublishedRevision = revision;
        entity.PublishedUrl = publishedUrl;
        entity.FilePath = filePath;
        entity.CommitSha = commitSha;
        entity.PublishedUtc = publishedUtc;
        entity.LastPublishError = null;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;
        await Posts.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
        return (await GetPostAsync(postId, cancellationToken))!;
    }

    public async Task<PostRecord> MarkPublishFailedAsync(string postId, int revision, string error, CancellationToken cancellationToken)
    {
        var entity = await GetPostEntityAsync(postId, cancellationToken);
        if (entity.WorkingRevision == revision)
        {
            entity.State = PostStates.PublishFailed;
            entity.LastPublishError = error;
            entity.UpdatedUtc = DateTimeOffset.UtcNow;
            await Posts.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
        }

        return (await GetPostAsync(postId, cancellationToken))!;
    }

    public async Task<PostRecord> SetDeletedAsync(string postId, bool deleted, CancellationToken cancellationToken)
    {
        var entity = await GetPostEntityAsync(postId, cancellationToken);
        entity.DeletedUtc = deleted ? DateTimeOffset.UtcNow : null;
        entity.State = deleted
            ? PostStates.Deleted
            : entity.PublishedRevision is null || entity.WorkingRevision > entity.PublishedRevision
                ? PostStates.Draft
                : PostStates.Published;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;
        await Posts.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
        return (await GetPostAsync(postId, cancellationToken))!;
    }

    public async Task<(PostRecord Post, bool Created)> ImportPostAsync(ImportedPost post, bool overwrite, CancellationToken cancellationToken)
    {
        var existing = await GetPostByRepositoryPathAsync(post.RepositoryPath, cancellationToken) ??
            await GetPostByUrlAsync(post.PublishedUrl, cancellationToken);

        if (existing is not null && !overwrite)
        {
            return (existing, false);
        }

        PostRecord record;
        if (existing is null)
        {
            record = await CreatePostAsync(
                new NewPost(post.Title, post.Content, post.Summary, post.Categories, post.Slug, post.PostType, post.PublishedUtc, post.Properties),
                cancellationToken);
        }
        else
        {
            record = await UpdatePostAsync(
                existing.Id,
                new PostUpdate(post.Title, post.Content, post.Summary, post.Categories, post.Slug, post.PostType, post.PublishedUtc, post.Properties),
                existing.ETag,
                cancellationToken);
        }

        var entity = await GetPostEntityAsync(record.Id, cancellationToken);
        entity.SourceRepositoryPath = post.RepositoryPath;
        entity.SourceCommitSha = post.CommitSha;
        entity.FilePath = post.Draft ? null : post.RepositoryPath;
        entity.CommitSha = post.Draft ? null : post.CommitSha;
        entity.PublishedUrl = post.Draft ? null : post.PublishedUrl;
        entity.PublishedUtc = post.Draft ? null : post.PublishedUtc;
        entity.PublishedRevision = post.Draft ? null : entity.WorkingRevision;
        entity.State = post.Draft ? PostStates.Draft : PostStates.Published;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;
        await Posts.UpdateEntityAsync(entity, entity.ETag, TableUpdateMode.Replace, cancellationToken);
        return ((await GetPostAsync(record.Id, cancellationToken))!, existing is null);
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
            await Posts.CreateIfNotExistsAsync(cancellationToken);
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

    private TableClient Posts => _posts ?? throw new InvalidOperationException("App:Storage:ConnectionString is not configured.");

    private async Task<PostEntity> GetPostEntityAsync(string postId, CancellationToken cancellationToken)
    {
        var response = await Posts.GetEntityIfExistsAsync<PostEntity>(
            SitePartition,
            PostRowKey(postId),
            cancellationToken: cancellationToken);
        return response.HasValue
            ? response.Value!
            : throw new InvalidOperationException($"Post '{postId}' was not found.");
    }

    private async Task<IReadOnlyList<PostEntity>> QueryPostEntitiesAsync(CancellationToken cancellationToken)
    {
        var entities = new List<PostEntity>();
        await foreach (var entity in Posts.QueryAsync<PostEntity>(
            filter: "PartitionKey eq 'site' and RowKey ge 'post:' and RowKey lt 'post;'",
            cancellationToken: cancellationToken))
        {
            entities.Add(entity);
        }

        return entities;
    }

    private async Task SubmitRevisionAsync(
        PostEntity post,
        string content,
        bool addPost,
        ETag expectedETag,
        CancellationToken cancellationToken)
    {
        var revision = new PostRevisionEntity
        {
            PartitionKey = SitePartition,
            RowKey = RevisionRowKey(post.Id, post.WorkingRevision),
            PostId = post.Id,
            Revision = post.WorkingRevision,
            Title = post.Title,
            Summary = post.Summary,
            CategoriesJson = post.CategoriesJson,
            PropertiesJson = post.PropertiesJson,
            Slug = post.Slug,
            PostType = post.PostType,
            RequestedPublishedUtc = post.RequestedPublishedUtc,
            CreatedUtc = post.UpdatedUtc
        };
        var actions = new List<TableTransactionAction>
        {
            addPost
                ? new(TableTransactionActionType.Add, post)
                : new(TableTransactionActionType.UpdateReplace, post, expectedETag),
            new(TableTransactionActionType.Add, revision)
        };

        var chunks = ChunkContent(content);
        for (var index = 0; index < chunks.Count; index++)
        {
            actions.Add(new TableTransactionAction(
                TableTransactionActionType.Add,
                new PostContentEntity
                {
                    PartitionKey = SitePartition,
                    RowKey = ContentRowKey(post.Id, post.WorkingRevision, index),
                    Content = chunks[index]
                }));
        }

        await Posts.SubmitTransactionAsync(actions, cancellationToken);
    }

    private async Task<string> ReadRevisionContentAsync(string postId, int revision, CancellationToken cancellationToken)
    {
        var prefix = ContentRowPrefix(postId, revision);
        var upper = $"{prefix};";
        var chunks = new List<PostContentEntity>();
        await foreach (var entity in Posts.QueryAsync<PostContentEntity>(
            filter: $"PartitionKey eq '{SitePartition}' and RowKey ge '{prefix}' and RowKey lt '{upper}'",
            cancellationToken: cancellationToken))
        {
            chunks.Add(entity);
        }

        return string.Concat(chunks.OrderBy(chunk => chunk.RowKey).Select(chunk => chunk.Content));
    }

    private static IReadOnlyList<string> ChunkContent(string content)
    {
        if (content.Length == 0)
        {
            return [string.Empty];
        }

        var chunks = new List<string>();
        for (var offset = 0; offset < content.Length; offset += ContentChunkCharacters)
        {
            chunks.Add(content.Substring(offset, Math.Min(ContentChunkCharacters, content.Length - offset)));
        }

        return chunks;
    }

    private static void ValidateContent(string content)
    {
        if (content.Length > MaximumContentCharacters)
        {
            throw new InvalidOperationException($"Post content exceeds the {MaximumContentCharacters:N0} character limit.");
        }
    }

    private static string SerializeProperties(IReadOnlyDictionary<string, IReadOnlyList<string>>? properties)
    {
        return System.Text.Json.JsonSerializer.Serialize(
            properties ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase));
    }

    private static string PostRowKey(string id) => $"post:{id}";
    private static string RevisionRowKey(string id, int revision) => $"revision:{id}:{revision:D10}";
    private static string ContentRowPrefix(string id, int revision) => $"body:{id}:{revision:D10}:";
    private static string ContentRowKey(string id, int revision, int chunk) => $"{ContentRowPrefix(id, revision)}{chunk:D3}";

    private static string NormalizeTablePrefix(string? prefix)
    {
        var normalized = new string((prefix ?? "LittlePublisher").Where(char.IsLetterOrDigit).ToArray());

        if (normalized.Length < 3)
        {
            normalized = "LittlePublisher";
        }

        return normalized.Length <= 40 ? normalized : normalized[..40];
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

    private sealed class PostEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public string Id { get; set; } = default!;
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string CategoriesJson { get; set; } = "[]";
        public string PropertiesJson { get; set; } = "{}";
        public string Slug { get; set; } = default!;
        public string PostType { get; set; } = default!;
        public string State { get; set; } = PostStates.Draft;
        public int WorkingRevision { get; set; }
        public int? PublishedRevision { get; set; }
        public DateTimeOffset? RequestedPublishedUtc { get; set; }
        public DateTimeOffset? PublishedUtc { get; set; }
        public string? PublishedUrl { get; set; }
        public string? FilePath { get; set; }
        public string? CommitSha { get; set; }
        public string? LastPublishError { get; set; }
        public string? SourceRepositoryPath { get; set; }
        public string? SourceCommitSha { get; set; }
        public DateTimeOffset? DeletedUtc { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
        public DateTimeOffset UpdatedUtc { get; set; }

        public PostRecord ToRecord(string content)
        {
            var categories = System.Text.Json.JsonSerializer.Deserialize<IReadOnlyList<string>>(CategoriesJson) ?? [];
            var properties = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, IReadOnlyList<string>>>(
                PropertiesJson) ?? new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
            return new(
                Id, Title, content, Summary, categories, Slug, PostType, State, WorkingRevision,
                PublishedRevision, RequestedPublishedUtc, PublishedUtc, PublishedUrl, FilePath, CommitSha,
                LastPublishError, SourceRepositoryPath, SourceCommitSha, CreatedUtc, UpdatedUtc, ETag.ToString(),
                properties, DeletedUtc);
        }
    }

    private sealed class PostRevisionEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public string PostId { get; set; } = default!;
        public int Revision { get; set; }
        public string? Title { get; set; }
        public string? Summary { get; set; }
        public string CategoriesJson { get; set; } = "[]";
        public string PropertiesJson { get; set; } = "{}";
        public string Slug { get; set; } = default!;
        public string PostType { get; set; } = default!;
        public DateTimeOffset? RequestedPublishedUtc { get; set; }
        public DateTimeOffset CreatedUtc { get; set; }
    }

    private sealed class PostContentEntity : ITableEntity
    {
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}
