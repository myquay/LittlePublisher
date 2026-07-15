using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace LittlePublisher.Web.Services.Webmentions;

public interface IWebmentionExtractor
{
    Task<ExtractedWebmention> ExtractAsync(SafeFetchResult fetch, string submittedTarget, CancellationToken cancellationToken);
}

public interface IWebmentionEndpointDiscovery
{
    Task<EndpointDiscoveryResult?> DiscoverAsync(SafeFetchResult fetch, CancellationToken cancellationToken);
}

public sealed class WebmentionHtml : IWebmentionExtractor, IWebmentionEndpointDiscovery
{
    private readonly HtmlParser _parser = new();

    public async Task<ExtractedWebmention> ExtractAsync(SafeFetchResult fetch, string submittedTarget, CancellationToken cancellationToken)
    {
        if (fetch.StatusCode == 410) throw new WebmentionWithdrawnException("Source returned 410 Gone.");
        if (fetch.StatusCode == 404) throw new SafeFetchException("Source returned 404 Not Found.", true);
        if (fetch.StatusCode is >= 500 or 429) throw new SafeFetchException($"Source returned HTTP {fetch.StatusCode}.", true);
        if (fetch.StatusCode is < 200 or >= 300) throw new SafeFetchException($"Source returned HTTP {fetch.StatusCode}.", false);

        var document = await _parser.ParseDocumentAsync(fetch.Body, cancellationToken);
        var matchingLinks = document.QuerySelectorAll("a[href], link[href]")
            .Where(element => TryResolve(fetch.FinalUri, element.GetAttribute("href"), out var resolved) && UrlEquals(resolved!, submittedTarget))
            .ToArray();
        if (matchingLinks.Length == 0) throw new WebmentionWithdrawnException("The source no longer links to the submitted target.");

        var entry = document.QuerySelector(".h-entry") ?? document.Body;
        var type = Classify(entry, matchingLinks, fetch.FinalUri, submittedTarget);
        var presentation = type is "like" or "reply" or "mention" ? type : "mention";
        var authorRoot = entry?.QuerySelector(".p-author.h-card, .h-card.p-author, .p-author") ?? document.QuerySelector(".h-card");
        var authorName = TextOrValue(authorRoot?.QuerySelector(".p-name")) ?? Clean(authorRoot?.TextContent);
        var authorUrlElement = authorRoot?.ClassList.Contains("u-url") == true ? authorRoot : authorRoot?.QuerySelector(".u-url");
        var authorUrl = ResolveAttribute(fetch.FinalUri, authorUrlElement, "href");
        var title = TextOrValue(entry?.QuerySelector(".p-name")) ?? document.Title;
        var content = Clean(entry?.QuerySelector(".e-content")?.TextContent ?? entry?.QuerySelector(".p-content")?.TextContent ?? entry?.TextContent) ?? string.Empty;
        content = content.Length <= 1000 ? content : content[..1000].TrimEnd() + "…";
        var publishedText = entry?.QuerySelector(".dt-published")?.GetAttribute("datetime") ?? entry?.QuerySelector(".dt-published")?.TextContent;
        DateTimeOffset? published = DateTimeOffset.TryParse(publishedText, out var parsed) ? parsed : null;
        var canonical = string.Join('|', type, authorName, authorUrl, title, content, published?.ToUniversalTime().ToString("O"));
        var hash = "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return new(type, presentation, authorName, authorUrl, Clean(title), content, published, hash, $"Found {matchingLinks.Length} exact target link(s) at {fetch.FinalUri}.");
    }

    public async Task<EndpointDiscoveryResult?> DiscoverAsync(SafeFetchResult fetch, CancellationToken cancellationToken)
    {
        if (fetch.Headers.TryGetValue("Link", out var links))
        {
            foreach (var header in links)
            {
                foreach (Match match in Regex.Matches(header, "<(?<url>[^>]+)>\\s*;(?<params>[^,]+)", RegexOptions.IgnoreCase))
                {
                    var parameters = match.Groups["params"].Value;
                    if (Regex.IsMatch(parameters, "rel\\s*=\\s*(?:\"[^\"]*\\bwebmention\\b[^\"]*\"|[^;,]*\\bwebmention\\b)", RegexOptions.IgnoreCase) && TryResolve(fetch.FinalUri, match.Groups["url"].Value, out var endpoint))
                        return new(endpoint!.AbsoluteUri, "http-link");
                }
            }
        }

        if (!fetch.ContentType.Contains("html", StringComparison.OrdinalIgnoreCase)) return null;
        var document = await _parser.ParseDocumentAsync(fetch.Body, cancellationToken);
        foreach (var selector in new[] { "link[rel][href]", "a[rel][href]" })
        {
            foreach (var element in document.QuerySelectorAll(selector))
            {
                var rel = (element.GetAttribute("rel") ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (rel.Contains("webmention", StringComparer.OrdinalIgnoreCase) && TryResolve(fetch.FinalUri, element.GetAttribute("href"), out var endpoint))
                    return new(endpoint!.AbsoluteUri, selector.StartsWith("link", StringComparison.Ordinal) ? "html-link" : "html-anchor");
            }
        }
        return null;
    }

    private static string Classify(IElement? entry, IElement[] matchingLinks, Uri source, string target)
    {
        if (HasProperty(entry, ".p-rsvp")) return "rsvp";
        if (HasTargetProperty(entry, ".u-repost-of", source, target)) return "repost";
        if (HasTargetProperty(entry, ".u-like-of", source, target)) return "like";
        if (HasTargetProperty(entry, ".u-in-reply-to", source, target)) return "reply";
        if (matchingLinks.Any(x => x.ClassList.Contains("u-like-of"))) return "like";
        if (matchingLinks.Any(x => x.ClassList.Contains("u-in-reply-to"))) return "reply";
        return "mention";
    }

    private static bool HasProperty(IElement? entry, string selector) => entry?.QuerySelector(selector) is not null;
    private static bool HasTargetProperty(IElement? entry, string selector, Uri source, string target) => entry?.QuerySelectorAll(selector).Any(x => TryResolve(source, x.GetAttribute("href") ?? x.GetAttribute("value"), out var url) && UrlEquals(url!, target)) == true;
    private static bool TryResolve(Uri baseUri, string? value, out Uri? result)
    {
        if (Uri.TryCreate(value, UriKind.Absolute, out result) && result.Scheme is "http" or "https") return true;
        return Uri.TryCreate(baseUri, value, out result) && result.Scheme is "http" or "https";
    }
    private static bool UrlEquals(Uri candidate, string target)
    {
        try { return string.Equals(WebmentionKeys.NormalizeUrl(candidate.AbsoluteUri), WebmentionKeys.NormalizeUrl(target), StringComparison.Ordinal); }
        catch { return false; }
    }
    private static string? ResolveAttribute(Uri baseUri, IElement? element, string attribute) => TryResolve(baseUri, element?.GetAttribute(attribute), out var uri) && uri?.Scheme is "http" or "https" ? uri.AbsoluteUri : null;
    private static string? TextOrValue(IElement? element) => Clean(element?.GetAttribute("value") ?? element?.GetAttribute("title") ?? element?.TextContent);
    private static string? Clean(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return Regex.Replace(value, "\\s+", " ").Trim();
    }
}

public sealed class WebmentionWithdrawnException(string message) : Exception(message);
