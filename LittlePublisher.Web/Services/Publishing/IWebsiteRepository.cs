namespace LittlePublisher.Web.Services.Publishing;

public interface IWebsiteRepository
{
    Task<string> PublishFileAsync(string relativePath, string content, string commitMessage, CancellationToken cancellationToken);

    Task<RepositoryMutationResult> MutateFilesAsync(IReadOnlyList<RepositoryFileMutation> mutations, string commitMessage, CancellationToken cancellationToken) =>
        throw new NotSupportedException("This repository implementation does not support atomic mutations.");

    Task<IReadOnlyList<WebsiteContentFile>> GetContentFilesAsync(CancellationToken cancellationToken);

    Task CheckConnectionAsync(CancellationToken cancellationToken);
}

public sealed record RepositoryFileMutation(string RelativePath, string? Content, byte[]? BinaryContent = null)
{
    public static RepositoryFileMutation Upsert(string relativePath, string content) => new(relativePath, content);
    public static RepositoryFileMutation UpsertBinary(string relativePath, byte[] content) => new(relativePath, null, content);
    public static RepositoryFileMutation Delete(string relativePath) => new(relativePath, null);
}

public sealed record RepositoryMutationResult(string CommitSha, IReadOnlyList<string> ChangedPaths, bool Changed);
