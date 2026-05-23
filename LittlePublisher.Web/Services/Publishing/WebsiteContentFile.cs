namespace LittlePublisher.Web.Services.Publishing;

public record WebsiteContentFile(
    string RelativePath,
    string Content,
    string? CommitSha);
