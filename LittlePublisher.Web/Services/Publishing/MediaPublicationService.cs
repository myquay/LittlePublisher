using LittlePublisher.Web.Configuration;

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

    public MediaPublicationService(AppConfiguration config, IWebsiteRepository repository)
    {
        _config = config;
        _repository = repository;
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
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        if (contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) &&
            !HasExpectedImageSignature(contentType, bytes))
        {
            throw new MediaPublicationException(
                StatusCodes.Status415UnsupportedMediaType,
                "The file contents do not match the declared image type.");
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
