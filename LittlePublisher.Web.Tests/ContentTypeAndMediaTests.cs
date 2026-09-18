using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Services.Publishing;

namespace LittlePublisher.Web.Tests;

public class ContentTypeAndMediaTests
{
    [Fact]
    public void PhotoProperties_AreNormalizedAndRequirePhotoAndAltAtPublishTime()
    {
        var properties = ContentTypeCatalog.NormalizeProperties(
            "photo",
            new Dictionary<string, IReadOnlyList<string>>
            {
                ["PHOTO"] = [" https://example.com/photo.jpg "],
                ["alt"] = [" Snow on the mountain "]
            });

        Assert.Equal(["https://example.com/photo.jpg"], properties["photo"]);
        Assert.Equal(["Snow on the mountain"], properties["alt"]);
        Assert.Null(ContentTypeCatalog.Validate("photo", properties, requireComplete: true));

        var incomplete = ContentTypeCatalog.NormalizeProperties(
            "photo",
            new Dictionary<string, IReadOnlyList<string>> { ["photo"] = ["/media/photo.jpg"] });
        Assert.Contains("alt", ContentTypeCatalog.Validate("photo", incomplete, requireComplete: true));
        Assert.Null(ContentTypeCatalog.Validate("photo", incomplete, requireComplete: false));
    }

    [Fact]
    public void NormalizeProperties_RejectsPropertiesFromAnotherType()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            ContentTypeCatalog.NormalizeProperties(
                "article",
                new Dictionary<string, IReadOnlyList<string>> { ["photo"] = ["/media/photo.jpg"] }));

        Assert.Contains("not supported", exception.Message);
    }

    [Fact]
    public async Task MediaPublicationService_StagesValidatedImageWithoutPublishing()
    {
        var repository = new CapturingRepository();
        var storage = new StagedMediaTests.MemoryStorage();
        var service = new MediaPublicationService(
            new AppConfiguration { Host = "https://publisher.example.com", Website = new WebsiteConfiguration { Url = "https://example.com" } },
            repository, storage);
        await using var content = new MemoryStream([0xff, 0xd8, 0xff, 0x01]);

        var result = await service.PublishAsync("image/jpeg", content.Length, content, CancellationToken.None);

        Assert.StartsWith("blog/static/media/", result.RepositoryPath);
        Assert.EndsWith(".jpg", result.RepositoryPath);
        Assert.StartsWith("https://publisher.example.com/api/media/staged/", result.Url);
        Assert.Empty(repository.Mutations);
        Assert.Equal([0xff, 0xd8, 0xff, 0x01], Assert.Single(storage.Items).Value.Content);
    }

    [Fact]
    public async Task MediaPublicationService_RejectsAnImageWithTheWrongSignature()
    {
        var service = new MediaPublicationService(
            new AppConfiguration { Website = new WebsiteConfiguration { Url = "https://example.com" } },
            new CapturingRepository());
        await using var content = new MemoryStream("not a jpeg"u8.ToArray());

        var exception = await Assert.ThrowsAsync<MediaPublicationException>(() =>
            service.PublishAsync("image/jpeg", content.Length, content, CancellationToken.None));

        Assert.Equal(415, exception.StatusCode);
    }

    private sealed class CapturingRepository : IWebsiteRepository
    {
        public IReadOnlyList<RepositoryFileMutation> Mutations { get; private set; } = [];

        public Task<RepositoryMutationResult> MutateFilesAsync(
            IReadOnlyList<RepositoryFileMutation> mutations,
            string commitMessage,
            CancellationToken cancellationToken)
        {
            Mutations = mutations;
            return Task.FromResult(new RepositoryMutationResult("commit", mutations.Select(x => x.RelativePath).ToArray(), true));
        }

        public Task<string> PublishFileAsync(string relativePath, string content, string commitMessage, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<WebsiteContentFile>> GetContentFilesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WebsiteContentFile>>([]);

        public Task CheckConnectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
