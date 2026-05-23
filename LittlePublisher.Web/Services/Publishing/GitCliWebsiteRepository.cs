using System.Diagnostics;
using System.Text.RegularExpressions;
using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Services.Publishing;

public class GitCliWebsiteRepository : IWebsiteRepository
{
    private readonly GitHubConfiguration _config;

    public GitCliWebsiteRepository(AppConfiguration config)
    {
        _config = config.GitHub;
    }

    public async Task<string> PublishFileAsync(string relativePath, string content, string commitMessage, CancellationToken cancellationToken)
    {
        ValidateConfiguration();
        ValidateRelativePath(relativePath);

        var checkoutPath = CreateCheckoutPath();

        try
        {
            await CloneAsync(checkoutPath, cancellationToken);

            await RunGitAsync(checkoutPath, ["config", "user.name", "LittlePublisher"], cancellationToken);
            await RunGitAsync(checkoutPath, ["config", "user.email", "littlepublisher@localhost"], cancellationToken);

            var fullPath = Path.Combine(checkoutPath, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content, cancellationToken);

            await RunGitAsync(checkoutPath, ["add", "--", relativePath], cancellationToken);
            await RunGitAsync(checkoutPath, ["commit", "-m", commitMessage], cancellationToken);
            await RunGitAsync(checkoutPath, ["push", "origin", _config.Branch], cancellationToken);

            return await RunGitAsync(checkoutPath, ["rev-parse", "HEAD"], cancellationToken);
        }
        finally
        {
            DeleteCheckout(checkoutPath);
        }
    }

    public async Task<IReadOnlyList<WebsiteContentFile>> GetContentFilesAsync(CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        var checkoutPath = CreateCheckoutPath();

        try
        {
            await CloneAsync(checkoutPath, cancellationToken);

            var contentPath = _config.ContentPath.Trim('/');
            ValidateRelativePath(contentPath);

            var fullContentPath = Path.Combine(checkoutPath, contentPath);

            if (!Directory.Exists(fullContentPath))
            {
                return Array.Empty<WebsiteContentFile>();
            }

            var commitSha = await RunGitAsync(checkoutPath, ["rev-parse", "HEAD"], cancellationToken);
            var files = Directory
                .EnumerateFiles(fullContentPath, "*", SearchOption.AllDirectories)
                .Where(IsImportableMarkdownFile)
                .Order(StringComparer.OrdinalIgnoreCase)
                .Select(file => new WebsiteContentFile(
                    RelativePath: Path.GetRelativePath(checkoutPath, file).Replace(Path.DirectorySeparatorChar, '/'),
                    Content: File.ReadAllText(file),
                    CommitSha: commitSha))
                .ToArray();

            return files;
        }
        finally
        {
            DeleteCheckout(checkoutPath);
        }
    }

    public async Task CheckConnectionAsync(CancellationToken cancellationToken)
    {
        ValidateConfiguration();

        var output = await RunGitAsync(
            workingDirectory: Path.GetTempPath(),
            arguments: ["ls-remote", "--heads", BuildAuthenticatedRemoteUrl(), _config.Branch],
            cancellationToken);

        if (string.IsNullOrWhiteSpace(output))
        {
            throw new InvalidOperationException($"Git branch '{_config.Branch}' was not found.");
        }
    }

    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_config.RepositoryUrl))
        {
            throw new InvalidOperationException("App:GitHub:RepositoryUrl is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_config.Branch))
        {
            throw new InvalidOperationException("App:GitHub:Branch is not configured.");
        }

        if (string.IsNullOrWhiteSpace(_config.Token))
        {
            throw new InvalidOperationException("App:GitHub:Token is not configured.");
        }
    }

    private string BuildAuthenticatedRemoteUrl()
    {
        if (!Uri.TryCreate(_config.RepositoryUrl, UriKind.Absolute, out var repositoryUri) ||
            repositoryUri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("App:GitHub:RepositoryUrl must be an HTTPS Git URL.");
        }

        var builder = new UriBuilder(repositoryUri)
        {
            UserName = string.IsNullOrWhiteSpace(_config.Username) ? "x-access-token" : _config.Username,
            Password = _config.Token
        };

        return builder.Uri.ToString();
    }

    private string CreateCheckoutPath()
    {
        var checkoutPath = Path.Combine(Path.GetTempPath(), "LittlePublisher", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(checkoutPath);

        return checkoutPath;
    }

    private async Task CloneAsync(string checkoutPath, CancellationToken cancellationToken)
    {
        await RunGitAsync(
            workingDirectory: Path.GetTempPath(),
            arguments: ["clone", "--branch", _config.Branch, "--single-branch", BuildAuthenticatedRemoteUrl(), checkoutPath],
            cancellationToken);
    }

    private static bool IsImportableMarkdownFile(string path)
    {
        var extension = Path.GetExtension(path);

        if (!string.Equals(extension, ".md", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(extension, ".markdown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.Equals(Path.GetFileNameWithoutExtension(path), "_index", StringComparison.OrdinalIgnoreCase);
    }

    private static void DeleteCheckout(string checkoutPath)
    {
        if (Directory.Exists(checkoutPath))
        {
            Directory.Delete(checkoutPath, recursive: true);
        }
    }

    private static void ValidateRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath) ||
            Path.IsPathRooted(relativePath) ||
            relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
        {
            throw new InvalidOperationException("Generated content path is not a safe repository-relative path.");
        }
    }

    private static async Task<string> RunGitAsync(string workingDirectory, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Unable to start git.");

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"git {arguments[0]} failed: {Redact(error)}");
        }

        return output.Trim();
    }

    private static string Redact(string value)
    {
        return Regex.Replace(value, "https://[^\\s/]+@", "https://***@", RegexOptions.IgnoreCase);
    }
}
