using System.Text.Json;
using LittlePublisher.Web.Services.Storage;

namespace LittlePublisher.Web.Services.Publishing;

public class ContentImportService : IContentImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IWebsiteRepository _websiteRepository;
    private readonly IPublisherStorage _storage;
    private readonly MarkdownPublishedItemParser _parser;

    public ContentImportService(
        IWebsiteRepository websiteRepository,
        IPublisherStorage storage,
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
                errors.Add(new ImportRepositoryError(file.RelativePath, ex.Message));
                continue;
            }

            if (!request.Overwrite)
            {
                var existing = await _storage.GetPublishedItemByUrlAsync(item.Url, cancellationToken);

                if (existing is not null)
                {
                    skipped++;
                    continue;
                }
            }

            if (!request.DryRun)
            {
                await _storage.SavePublishedItemAsync(
                    new NewPublishedItem(
                        Url: item.Url,
                        Title: item.Title,
                        Content: item.Content,
                        Categories: item.Categories,
                        PublishedUtc: item.PublishedUtc,
                        FilePath: item.FilePath,
                        CommitSha: item.CommitSha,
                        PropertiesJson: BuildPropertiesJson(item),
                        Draft: item.Draft),
                    cancellationToken);
            }

            imported++;
        }

        return new ImportRepositoryResult(
            Scanned: files.Count,
            Imported: imported,
            Skipped: skipped,
            Failed: failed,
            Errors: errors);
    }

    private static bool IsHugoIndexFile(string relativePath)
    {
        return string.Equals(Path.GetFileNameWithoutExtension(relativePath), "_index", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildPropertiesJson(ParsedPublishedItem item)
    {
        var properties = new Dictionary<string, object>
        {
            ["content"] = new[] { item.Content },
            ["url"] = new[] { item.Url }
        };

        if (!item.Draft)
        {
            properties["published"] = new[] { item.PublishedUtc.ToString("O") };
        }

        if (!string.IsNullOrWhiteSpace(item.Title))
        {
            properties["name"] = new[] { item.Title };
        }

        if (!string.IsNullOrWhiteSpace(item.Summary))
        {
            properties["summary"] = new[] { item.Summary };
        }

        if (item.Categories.Count > 0)
        {
            properties["category"] = item.Categories;
        }

        return JsonSerializer.Serialize(
            new Dictionary<string, object>
            {
                ["type"] = new[] { "h-entry" },
                ["properties"] = properties
            },
            JsonOptions);
    }
}
