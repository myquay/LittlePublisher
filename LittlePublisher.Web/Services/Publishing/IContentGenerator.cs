namespace LittlePublisher.Web.Services.Publishing;

public interface IContentGenerator
{
    string GenerateMarkdown(PublishCreateRequest request, string url);
}
