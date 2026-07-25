using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Tests;

public class CorsConfigurationTests
{
    [Fact]
    public void GetAllowedOrigins_IncludesHostAndConfiguredEditors()
    {
        var config = new AppConfiguration
        {
            Host = "https://publisher.example.com",
            AllowedEditors =
            [
                "https://editor.example.com",
                "http://localhost:8080"
            ]
        };

        var origins = CorsConfiguration.GetAllowedOrigins(config);

        Assert.Equal(
            [
                "https://publisher.example.com",
                "https://editor.example.com",
                "http://localhost:8080"
            ],
            origins);
    }

    [Fact]
    public void GetAllowedOrigins_RemovesDuplicateOrigins()
    {
        var config = new AppConfiguration
        {
            Host = "https://publisher.example.com",
            AllowedEditors =
            [
                "https://editor.example.com",
                "HTTPS://EDITOR.EXAMPLE.COM"
            ]
        };

        var origins = CorsConfiguration.GetAllowedOrigins(config);

        Assert.Equal(
            ["https://publisher.example.com", "https://editor.example.com"],
            origins);
    }

    [Theory]
    [InlineData("editor.example.com")]
    [InlineData("ftp://editor.example.com")]
    [InlineData("https://editor.example.com/")]
    [InlineData("https://editor.example.com/write")]
    [InlineData("https://editor.example.com?mode=write")]
    [InlineData("https://user:password@editor.example.com")]
    public void GetAllowedOrigins_RejectsValuesThatAreNotExactHttpOrigins(string editor)
    {
        var config = new AppConfiguration
        {
            Host = "https://publisher.example.com",
            AllowedEditors = [editor]
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => CorsConfiguration.GetAllowedOrigins(config));

        Assert.Contains("App:AllowedEditors", exception.Message);
    }
}
