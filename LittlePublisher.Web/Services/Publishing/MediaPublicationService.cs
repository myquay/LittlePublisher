using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Storage;
using System.Text.RegularExpressions;

namespace LittlePublisher.Web.Services.Publishing;

public sealed class MediaPublicationService
{
    public const long MaximumUploadBytes = 20 * 1024 * 1024;
    public const long MaximumRequestBytes = MaximumUploadBytes + 64 * 1024;

    private static readonly IReadOnlyDictionary<string, string> SupportedTypes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["image/jpeg"] = ".jpg",
            ["image/png"] = ".png",
            ["image/gif"] = ".gif",
            ["image/webp"] = ".webp",
            ["audio/mpeg"] = ".mp3",
            ["audio/ogg"] = ".ogg",
            ["video/mp4"] = ".mp4",
            ["video/webm"] = ".webm"
        };

    private readonly AppConfiguration _config;
    private readonly IWebsiteRepository _repository;
    private readonly IStagedMediaStorage? _staging;

    public MediaPublicationService(AppConfiguration config, IWebsiteRepository repository, IStagedMediaStorage? staging = null)
    {
        _config = config;
        _repository = repository;
        _staging = staging;
    }

    public async Task<MediaPublicationResult> PublishAsync(
        string contentType,
        long length,
        Stream content,
        CancellationToken cancellationToken)
    {
        if (length <= 0)
        {
            throw new MediaPublicationException(StatusCodes.Status400BadRequest, "A non-empty file is required.");
        }

        if (length > MaximumUploadBytes)
        {
            throw new MediaPublicationException(StatusCodes.Status413PayloadTooLarge, "The file exceeds the 20 MB upload limit.");
        }

        if (!SupportedTypes.TryGetValue(contentType, out var extension))
        {
            throw new MediaPublicationException(StatusCodes.Status415UnsupportedMediaType, "The uploaded media type is not supported.");
        }

        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await content.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaximumUploadBytes)
                throw new MediaPublicationException(StatusCodes.Status413PayloadTooLarge, "The file exceeds the 20 MB upload limit.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
        }
        if (buffer.Length == 0)
            throw new MediaPublicationException(StatusCodes.Status400BadRequest, "A non-empty file is required.");
        var bytes = buffer.ToArray();
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
            !HasExpectedImageSignature(contentType, bytes))
        {
            throw new MediaPublicationException(
                StatusCodes.Status415UnsupportedMediaType,
                "The file contents do not match the declared image type.");
        }

        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            if (!Uri.TryCreate(_config.Host, UriKind.Absolute, out var host) || host.Scheme is not ("https" or "http"))
                throw new MediaPublicationException(StatusCodes.Status503ServiceUnavailable, "Configure the publisher's public host URL before uploading photos.");
            var id = $"{Guid.NewGuid():N}{extension}";
            await Staging.PutAsync(id, new StagedMedia(bytes, contentType), cancellationToken);
            return new MediaPublicationResult($"{StagingPrefix}{id}", $"blog/static/media/{id}");
        }

        var name = $"{DateTimeOffset.UtcNow:yyyy/MM}/{Guid.NewGuid():N}{extension}";
        var repositoryPath = $"blog/static/media/{name}";
        await _repository.MutateFilesAsync(
            [RepositoryFileMutation.UpsertBinary(repositoryPath, bytes)],
            $"Upload media {Path.GetFileName(name)}",
            cancellationToken);

        return new MediaPublicationResult(
            $"{_config.Website.Url.TrimEnd('/')}/media/{name}",
            repositoryPath);
    }

    private IStagedMediaStorage Staging => _staging ??
        throw new InvalidOperationException("Media staging storage is not configured.");

    private string StagingPrefix => $"{(_config.Host ?? "").TrimEnd('/')}/api/media/staged/";
    public static bool IsValidId(string id) => Regex.IsMatch(id, @"\A[a-f0-9]{32}\.(jpg|png|gif|webp)\z");

    public Task<StagedMedia?> GetStagedAsync(string id, CancellationToken cancellationToken) =>
        IsValidId(id) ? Staging.GetAsync(id, cancellationToken) : Task.FromResult<StagedMedia?>(null);

    public async Task<(PublishCreateRequest Request, IReadOnlyList<RepositoryFileMutation> Images)> PrepareAsync(
        PublishCreateRequest request, CancellationToken cancellationToken)
    {
        // Match only references issued by this publisher, never fetch arbitrary URLs.
        var pattern = new Regex(Regex.Escape(StagingPrefix) + @"(?<id>[a-f0-9]{32}\.(?:jpg|png|gif|webp))(?=$|[\s)\]<>""'])");
        var sources = new[] { request.Content, request.Summary ?? "" }
            .Concat(request.Properties?.Values.SelectMany(x => x) ?? []);
        var ids = sources.SelectMany(x => pattern.Matches(x).Select(m => m.Groups["id"].Value)).Distinct().ToArray();
        var images = new List<RepositoryFileMutation>();
        foreach (var id in ids)
        {
            var media = await GetStagedAsync(id, cancellationToken) ??
                throw new InvalidOperationException("A staged image is missing. Upload it again before publishing.");
            images.Add(RepositoryFileMutation.UpsertBinary($"blog/static/media/{id}", media.Content));
        }
        string Resolve(string value) => pattern.Replace(value, m => $"{_config.Website.Url.TrimEnd('/')}/media/{m.Groups["id"].Value}");
        return (request with
        {
            Content = Resolve(request.Content),
            Summary = request.Summary is null ? null : Resolve(request.Summary),
            Properties = request.Properties?.ToDictionary(x => x.Key,
                x => (IReadOnlyList<string>)x.Value.Select(Resolve).ToArray())
        }, images);
    }

    private static bool HasExpectedImageSignature(string contentType, byte[] bytes)
    {
        return contentType.ToLowerInvariant() switch
        {
            "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
            "image/png" => bytes.Length >= 8 &&
                bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a }),
            "image/gif" => bytes.Length >= 6 &&
                (bytes.AsSpan(0, 6).SequenceEqual("GIF87a"u8) || bytes.AsSpan(0, 6).SequenceEqual("GIF89a"u8)),
            "image/webp" => bytes.Length >= 12 &&
                bytes.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
                bytes.AsSpan(8, 4).SequenceEqual("WEBP"u8),
            _ => false
        };
    }
}

public sealed record MediaPublicationResult(string Url, string RepositoryPath);

public sealed class MediaPublicationException : Exception
{
    public MediaPublicationException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}
