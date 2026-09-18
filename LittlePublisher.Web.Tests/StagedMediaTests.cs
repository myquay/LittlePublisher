using LittlePublisher.Web.Configuration;
using LittlePublisher.Web.Controllers;
using LittlePublisher.Web.Services.Publishing;
using LittlePublisher.Web.Services.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LittlePublisher.Web.Tests;

public class StagedMediaTests
{
    internal sealed class MemoryStorage : IStagedMediaStorage
    {
        public Dictionary<string, StagedMedia> Items { get; } = [];
        public Task PutAsync(string id, StagedMedia media, CancellationToken ct) { Items.Add(id, media); return Task.CompletedTask; }
        public Task<StagedMedia?> GetAsync(string id, CancellationToken ct) => Task.FromResult(Items.GetValueOrDefault(id));
    }
    private sealed class Repository : IWebsiteRepository
    {
        public List<IReadOnlyList<RepositoryFileMutation>> Commits { get; } = [];
        public bool Fail { get; set; }
        public Task<RepositoryMutationResult> MutateFilesAsync(IReadOnlyList<RepositoryFileMutation> mutations, string message, CancellationToken ct)
        {
            if (Fail) throw new IOException("push failed");
            Commits.Add(mutations);
            return Task.FromResult(new RepositoryMutationResult("sha", mutations.Select(x => x.RelativePath).ToArray(), true));
        }
        public async Task<string> PublishFileAsync(string path, string content, string message, CancellationToken ct) =>
            (await MutateFilesAsync([RepositoryFileMutation.Upsert(path, content)], message, ct)).CommitSha;
        public Task<IReadOnlyList<WebsiteContentFile>> GetContentFilesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task CheckConnectionAsync(CancellationToken ct) => Task.CompletedTask;
    }
    private static AppConfiguration Config() => new()
    {
        Host = "https://publisher.example.com",
        Website = new() { Url = "https://example.com" },
        GitHub = new() { ContentPath = "blog/content" }
    };

    [Theory]
    [InlineData("article", "")]
    [InlineData("photo", "photo")]
    [InlineData("book-review", "book-cover")]
    public async Task UploadsStayPrivateAndAreCommittedWithReferencingContent(string type, string property)
    {
        var storage = new MemoryStorage();
        var repository = new Repository();
        var media = new MediaPublicationService(Config(), repository, storage);
        using var file = new MemoryStream([0xff, 0xd8, 0xff, 0x01]);
        var upload = await media.PublishAsync("image/jpeg", file.Length, file, default);
        Assert.Empty(repository.Commits);
        Assert.Single(storage.Items);
        Assert.StartsWith("https://publisher.example.com/api/media/staged/", upload.Url);
        var properties = new Dictionary<string, IReadOnlyList<string>>();
        if (property != "") properties[property] = [upload.Url];
        if (type == "book-review") { properties["book-title"] = ["Book"]; properties["book-author"] = ["Author"]; properties["rating"] = ["4"]; }
        if (type == "photo") properties["alt"] = ["Photo"];
        var request = new PublishCreateRequest("Title", $"![Photo]({upload.Url})", null, [], DateTimeOffset.UtcNow, "title", type, properties);
        var publisher = new PublishingService(Config(), new MarkdownContentGenerator(), repository, media);
        await publisher.PublishCreateAsync(request, default);
        var commit = Assert.Single(repository.Commits);
        Assert.Equal(2, commit.Count);
        var image = Assert.Single(commit, x => x.BinaryContent is not null);
        var markdown = Assert.Single(commit, x => x.Content is not null).Content!;
        Assert.DoesNotContain("/api/media/staged/", markdown);
        Assert.Contains($"https://example.com/media/{storage.Items.Single().Key}", markdown);
        Assert.Equal(upload.RepositoryPath, image.RelativePath);
        Assert.Equal(file.ToArray(), image.BinaryContent);
        // Draft references remain valid after publication and across retries.
        Assert.Single(storage.Items);
        Assert.Contains("/api/media/staged/", request.Content);
        await publisher.PublishCreateAsync(request, default);
        Assert.Equal(image.RelativePath, repository.Commits[1].Single(x => x.BinaryContent is not null).RelativePath);
    }

    [Fact]
    public async Task FailedPublicationRetainsStagedBytesAndMissingImagesPreventAnyCommit()
    {
        var storage = new MemoryStorage();
        var repository = new Repository { Fail = true };
        var media = new MediaPublicationService(Config(), repository, storage);
        using var file = new MemoryStream([0xff, 0xd8, 0xff]);
        var upload = await media.PublishAsync("image/jpeg", file.Length, file, default);
        var request = new PublishCreateRequest("Title", $"![Photo]({upload.Url})", null, [], DateTimeOffset.UtcNow, "title");
        var publisher = new PublishingService(Config(), new MarkdownContentGenerator(), repository, media);
        await Assert.ThrowsAsync<IOException>(() => publisher.PublishCreateAsync(request, default));
        Assert.Single(storage.Items);
        Assert.Empty(repository.Commits);
        repository.Fail = false;
        await publisher.PublishCreateAsync(request, default);
        Assert.Single(repository.Commits);
        storage.Items.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishCreateAsync(request, default));
        Assert.Single(repository.Commits);
    }

    [Fact]
    public async Task BothUploadEndpointsStagePhotosWithoutWritingToTheWebsite()
    {
        var storage = new MemoryStorage();
        var repository = new Repository();
        var media = new MediaPublicationService(Config(), repository, storage);
        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity([
            new System.Security.Claims.Claim("me", "https://example.com"),
            new System.Security.Claims.Claim("scope", "media create")], "test"));
        using var stream = new MemoryStream([0xff, 0xd8, 0xff]);
        var file = new FormFile(stream, 0, stream.Length, "file", "photo.jpg")
        { Headers = new HeaderDictionary(), ContentType = "image/jpeg" };
        var admin = new AdminMediaController(media);
        Assert.IsType<CreatedResult>(await admin.Upload(file, default));
        stream.Position = 0;
        var micropub = new MicropubMediaController(Config(), media)
        { ControllerContext = new() { HttpContext = context } };
        var result = Assert.IsType<CreatedResult>(await micropub.Upload(file, default));
        Assert.StartsWith("https://publisher.example.com/api/media/staged/", result.Location);
        Assert.Equal(result.Location, context.Response.Headers.Location);
        Assert.Equal(2, storage.Items.Count);
        Assert.Empty(repository.Commits);
    }

    [Fact]
    public async Task PreviewReturnsPrivateBytesAndRejectsInvalidIds()
    {
        var storage = new MemoryStorage();
        var media = new MediaPublicationService(Config(), new Repository(), storage);
        var id = new string('a', 32) + ".png";
        await storage.PutAsync(id, new StagedMedia([1, 2, 3], "image/png"), default);
        var controller = new AdminMediaController(media) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        var response = Assert.IsType<FileContentResult>(await controller.Preview(id, default));
        Assert.Equal("image/png", response.ContentType);
        Assert.Equal("private, no-store", controller.Response.Headers.CacheControl);
        Assert.IsType<NotFoundResult>(await controller.Preview("../invalid", default));
    }
}
