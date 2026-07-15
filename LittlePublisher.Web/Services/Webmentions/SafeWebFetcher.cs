using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using LittlePublisher.Web.Configuration;

namespace LittlePublisher.Web.Services.Webmentions;

public interface ISafeWebFetcher
{
    Task<SafeFetchResult> FetchAsync(Uri uri, CancellationToken cancellationToken);
    Task<SafeFetchResult> PostFormAsync(Uri uri, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken);
}

public sealed class SafeWebFetcher : ISafeWebFetcher, IDisposable
{
    private readonly WebmentionConfiguration _config;
    private readonly HttpClient _client;

    public SafeWebFetcher(AppConfiguration config)
    {
        _config = config.Webmention;
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            ConnectTimeout = TimeSpan.FromSeconds(Math.Clamp(_config.FetchTimeoutSeconds, 1, 15)),
            ConnectCallback = ConnectSafeAsync
        };
        _client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(Math.Clamp(_config.FetchTimeoutSeconds * (_config.MaxRedirects + 1), 5, 60))
        };
        _client.DefaultRequestHeaders.UserAgent.ParseAdd("LittlePublisher-Webmention/1.0 (+https://github.com/myquay/LittlePublisher)");
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
        _client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain", .5));
    }

    public async Task<SafeFetchResult> FetchAsync(Uri uri, CancellationToken cancellationToken)
    {
        ValidateUri(uri);
        var requested = uri;
        var current = uri;
        var redirects = new List<string>();

        for (var redirect = 0; redirect <= Math.Clamp(_config.MaxRedirects, 0, 10); redirect++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (IsRedirect(response.StatusCode))
            {
                if (redirect == _config.MaxRedirects || response.Headers.Location is null) throw new SafeFetchException("Too many redirects or a redirect without Location.", false);
                var next = response.Headers.Location.IsAbsoluteUri ? response.Headers.Location : new Uri(current, response.Headers.Location);
                ValidateUri(next);
                redirects.Add(next.AbsoluteUri);
                current = next;
                continue;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType?.ToLowerInvariant() ?? string.Empty;
            var successful = (int)response.StatusCode is >= 200 and < 300;
            if (successful && contentType is not ("text/html" or "application/xhtml+xml" or "text/plain")) throw new SafeFetchException($"Unsupported response content type '{contentType}'.", false);
            var body = response.Content.Headers.ContentLength == 0 ? string.Empty : await ReadBoundedAsync(response.Content, cancellationToken);
            var headers = response.Headers.Concat(response.Content.Headers)
                .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.SelectMany(x => x.Value).ToArray(), StringComparer.OrdinalIgnoreCase);
            return new(requested, current, (int)response.StatusCode, contentType, body, redirects, headers);
        }

        throw new SafeFetchException("Redirect limit exceeded.", false);
    }

    public async Task<SafeFetchResult> PostFormAsync(Uri uri, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken)
    {
        ValidateUri(uri);
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new FormUrlEncodedContent(values) };
        using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = string.Empty;
        if (response.Content.Headers.ContentLength is null or > 0) body = await ReadBoundedAsync(response.Content, cancellationToken);
        var headers = response.Headers.Concat(response.Content.Headers)
            .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.SelectMany(x => x.Value).ToArray(), StringComparer.OrdinalIgnoreCase);
        return new(uri, uri, (int)response.StatusCode, response.Content.Headers.ContentType?.MediaType ?? string.Empty, body, [], headers);
    }

    private async ValueTask<Stream> ConnectSafeAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken);
        var address = addresses.FirstOrDefault(IsPublicAddress) ?? throw new SafeFetchException("The host does not resolve to a public address.", false);
        var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch { socket.Dispose(); throw; }
    }

    private void ValidateUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri || uri.Scheme is not ("http" or "https") || !string.IsNullOrEmpty(uri.UserInfo)) throw new SafeFetchException("Only absolute HTTP(S) URLs without credentials are allowed.", false);
        if (!_config.AllowedPorts.Contains(uri.Port)) throw new SafeFetchException("The URL uses a port that is not allowed.", false);
        if (IPAddress.TryParse(uri.IdnHost, out var literal) && !IsPublicAddress(literal)) throw new SafeFetchException("The URL resolves to a non-public address.", false);
    }

    private async Task<string> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        var max = Math.Clamp(_config.MaxResponseBytes, 16_384, 2_097_152);
        await using var stream = await content.ReadAsStreamAsync(cancellationToken);
        using var memory = new MemoryStream();
        var buffer = new byte[16_384];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0) break;
            if (memory.Length + read > max) throw new SafeFetchException("The response exceeded the configured size limit.", false);
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        memory.Position = 0;
        using var reader = new StreamReader(memory, content.Headers.ContentType?.CharSet is { } charset ? System.Text.Encoding.GetEncoding(charset) : System.Text.Encoding.UTF8, true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    internal static bool IsPublicAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;
        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            return !(bytes[0] == 10 || bytes[0] == 127 || bytes[0] == 0 ||
                (bytes[0] == 169 && bytes[1] == 254) || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
                (bytes[0] == 192 && bytes[1] == 168) || (bytes[0] == 100 && bytes[1] is >= 64 and <= 127) ||
                bytes[0] >= 224 || (bytes[0] == 192 && bytes[1] == 0 && bytes[2] == 2) ||
                (bytes[0] == 198 && bytes[1] == 51 && bytes[2] == 100) || (bytes[0] == 203 && bytes[1] == 0 && bytes[2] == 113));
        }
        return !(address.IsIPv6LinkLocal || address.IsIPv6Multicast || address.IsIPv6SiteLocal || (bytes[0] & 0xfe) == 0xfc || address.Equals(IPAddress.IPv6Loopback));
    }

    private static bool IsRedirect(HttpStatusCode status) => status is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.RedirectMethod or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;
    public void Dispose() => _client.Dispose();
}

public sealed class SafeFetchException(string message, bool retryable, Exception? inner = null) : Exception(message, inner)
{
    public bool Retryable { get; } = retryable;
}
