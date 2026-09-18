using LittlePublisher.Web.Services.Storage;

namespace LittlePublisher.Web.Services.Publishing;

public class ContentImportService : IContentImportService
{
    private readonly IWebsiteRepository _websiteRepository;
    private readonly IPostStorage _storage;
    private readonly MarkdownPublishedItemParser _parser;

    public ContentImportService(
        IWebsiteRepository websiteRepository,
        IPostStorage storage,
        MarkdownPublishedItemParser parser)
    {
        _websiteRepository = websiteRepository;
        _storage = storage;
        _parser = parser;
    }

    public async Task<ImportRepositoryResult> ImportRepositoryAsync(ImportRepositoryRequest request, CancellationToken cancellationToken)
    {
        var files = await _websiteRepository.GetContentFilesAsync(cancellationToken);
        var imported = 0;
        var skipped = 0;
        var failed = 0;
        var ambiguous = 0;
        var draftsInRepository = 0;
        var draftFiles = new List<string>();
        var errors = new List<ImportRepositoryError>();

        foreach (var file in files)
        {
            if (IsHugoIndexFile(file.RelativePath))
            {
                skipped++;
                continue;
            }

            ParsedPublishedItem item;

            try
            {
                item = _parser.Parse(file);
            }
            catch (InvalidOperationException ex)
            {
                failed++;
                if (ex.Message.Contains("ambiguous", StringComparison.OrdinalIgnoreCase))
                {
                    ambiguous++;
                }
                errors.Add(new ImportRepositoryError(file.RelativePath, ex.Message));
                continue;
            }

            if (item.PublishedUtc is { } publishedUtc &&
                publishedUtc > DateTimeOffset.UtcNow &&
                !item.Draft)
            {
                failed++;
                ambiguous++;
                errors.Add(new ImportRepositoryError(file.RelativePath, "Future-dated content requires an explicit scheduling decision."));
                continue;
            }

            if (item.Draft)
            {
                draftsInRepository++;
                draftFiles.Add(file.RelativePath);
            }

            if (!request.Overwrite)
            {
                var existing = await _storage.GetPostByRepositoryPathAsync(item.FilePath, cancellationToken) ??
                    await _storage.GetPostByUrlAsync(item.Url, cancellationToken);
                if (existing is not null)
                {
                    skipped++;
                    continue;
                }
            }

            if (!request.DryRun)
            {
                await _storage.ImportPostAsync(
                    new ImportedPost(
                        Title: item.Title,
                        Content: item.Content,
                        Summary: item.Summary,
                        Categories: item.Categories,
                        Slug: BuildSlug(item),
                        PostType: item.PostType,
                        Draft: item.Draft,
                        PublishedUtc: item.PublishedUtc,
                        PublishedUrl: item.Url,
                        RepositoryPath: item.FilePath,
                        CommitSha: item.CommitSha ?? string.Empty,
                        Properties: item.Properties),
                    request.Overwrite,
                    cancellationToken);
            }

            imported++;
        }

        return new ImportRepositoryResult(
            Scanned: files.Count,
            Imported: imported,
            Skipped: skipped,
            Failed: failed,
            Errors: errors,
            Ambiguous: ambiguous,
            DraftsInRepository: draftsInRepository,
            DraftFiles: draftFiles);
    }

    private static bool IsHugoIndexFile(string relativePath)
    {
        return string.Equals(Path.GetFileNameWithoutExtension(relativePath), "_index", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSlug(ParsedPublishedItem item)
    {
        var path = new Uri(item.Url).AbsolutePath.Trim('/');
        var candidate = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ??
            Path.GetFileNameWithoutExtension(item.FilePath);
        return PublishingService.BuildSlug(candidate, item.Content);
    }

}
