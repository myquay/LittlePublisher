using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace LittlePublisher.Web.Services.Publishing;

public sealed record BookSearchResult(string Id, string Title, string Author, string? CoverUrl,
    string Url, string? Isbn, string? Year);

// One singleton gate bounds aggregate upstream traffic, including simultaneous editors.
public sealed class OpenLibraryService(IHttpClientFactory clients) : IDisposable
{
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 200 });
    public void Dispose() { cache.Dispose(); gate.Dispose(); }
    private readonly SemaphoreSlim gate = new(1, 1);
    private DateTimeOffset nextRequest;

    public async Task<IReadOnlyList<BookSearchResult>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        query = query.Trim();
        var key = "openlibrary:" + query.ToLowerInvariant();
        if (cache.TryGetValue<IReadOnlyList<BookSearchResult>>(key, out var cached)) return cached!;
        // Do not accumulate an unbounded queue of upstream requests.
        if (!await gate.WaitAsync(0, cancellationToken))
            throw new HttpRequestException("A book search is already running. Please try again.");
        try
        {
            if (cache.TryGetValue<IReadOnlyList<BookSearchResult>>(key, out cached)) return cached!;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(25));
            var isbn = Regex.Replace(query, "[\\s-]", "").ToUpperInvariant();
            var isIsbn = Regex.IsMatch(isbn, @"^(\d{9}[\dX]|\d{13})$");
            IReadOnlyList<BookSearchResult> results;
            if (isIsbn) results = await ReadIsbnAsync(isbn, timeout.Token);
            else
            {
                using var json = await FetchAsync($"search.json?q={Uri.EscapeDataString(query)}&fields=key,title,author_name,cover_i,first_publish_year&limit=10", timeout.Token);
                results = ReadSearch(json.RootElement);
            }
            cache.Set(key, results, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6), Size = 1 });
            return results;
        }
        finally { gate.Release(); }
    }

    private static IReadOnlyList<BookSearchResult> ReadSearch(JsonElement root)
    {
        if (!root.TryGetProperty("docs", out var docs)) return [];
        return docs.EnumerateArray().Take(10).Select(doc =>
        {
            var id = Text(doc, "key");
            var cover = doc.TryGetProperty("cover_i", out var value) && value.TryGetInt64(out var number) && number > 0
                ? $"https://covers.openlibrary.org/b/id/{number}-L.jpg?default=false" : null;
            var authors = doc.TryGetProperty("author_name", out var names)
                ? string.Join(", ", names.EnumerateArray().Select(n => n.GetString())) : "";
            return new BookSearchResult(id, Text(doc, "title"), authors, cover,
                "https://openlibrary.org" + id, null,
                doc.TryGetProperty("first_publish_year", out var year) ? year.ToString() : null);
        }).Where(book => Regex.IsMatch(book.Id, @"^/works/OL\d+W$") && book.Title.Length > 0).ToArray();
    }

    // ISBNs resolve to editions. Never mix a work's arbitrary ISBN with another edition's cover.
    private async Task<IReadOnlyList<BookSearchResult>> ReadIsbnAsync(string isbn, CancellationToken cancellationToken)
    {
        JsonDocument edition;
        try { edition = await FetchAsync($"isbn/{isbn}.json", cancellationToken); }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return []; }
        using (edition)
        {
            var book = edition.RootElement;
            var authorKeys = AuthorKeys(book);
            if (authorKeys.Count == 0 && book.TryGetProperty("works", out var works) && works.GetArrayLength() > 0)
            {
                var workKey = Text(works[0], "key");
                if (Regex.IsMatch(workKey, @"^/works/OL\d+W$"))
                {
                    using var work = await FetchAsync(workKey.TrimStart('/') + ".json", cancellationToken);
                    authorKeys = AuthorKeys(work.RootElement);
                }
            }
            var authors = new List<string>();
            foreach (var authorKey in authorKeys.Take(10))
            {
                using var author = await FetchAsync(authorKey.TrimStart('/') + ".json", cancellationToken);
                var name = Text(author.RootElement, "name");
                if (name.Length > 0) authors.Add(name);
            }
            string? cover = null;
            if (book.TryGetProperty("covers", out var covers))
                foreach (var value in covers.EnumerateArray())
                    if (value.TryGetInt64(out var id) && id > 0)
                    {
                        cover = $"https://covers.openlibrary.org/b/id/{id}-L.jpg?default=false";
                        break;
                    }
            return [new("ISBN:" + isbn, Text(book, "title"), string.Join(", ", authors), cover,
                "https://openlibrary.org/isbn/" + isbn, isbn, Text(book, "publish_date"))];
        }
    }

    private static List<string> AuthorKeys(JsonElement book)
    {
        if (!book.TryGetProperty("authors", out var authors)) return [];
        return authors.EnumerateArray().Select(value => Text(value.TryGetProperty("author", out var nested) ? nested : value, "key"))
            .Where(key => Regex.IsMatch(key, @"^/authors/OL\d+A$")).Distinct().ToList();
    }

    private async Task<JsonDocument> FetchAsync(string path, CancellationToken cancellationToken)
    {
        var cacheKey = "document:" + path;
        if (cache.TryGetValue<string>(cacheKey, out var cached)) return JsonDocument.Parse(cached!);
        // ISBN endpoints redirect to /books/<edition>.json. Only follow validated Open Library redirects.
        for (var redirects = 0; redirects <= 2; redirects++)
        {
            var delay = nextRequest - DateTimeOffset.UtcNow;
            if (delay > TimeSpan.Zero) await Task.Delay(delay, cancellationToken);
            using var client = clients.CreateClient("OpenLibrary");
            nextRequest = DateTimeOffset.UtcNow.AddSeconds(1);
            using var response = await client.GetAsync(path, cancellationToken);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                var location = response.Headers.Location;
                var target = location is null ? null : new Uri(new Uri("https://openlibrary.org/" + path), location);
                if (target is null || target.Scheme != "https" || target.Host != "openlibrary.org" || !target.IsDefaultPort ||
                    !Regex.IsMatch(target.PathAndQuery, @"^/books/OL\d+M\.json$"))
                    throw new HttpRequestException("Unexpected Open Library redirect.");
                path = target.PathAndQuery.TrimStart('/');
                continue;
            }
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var parsed = JsonDocument.Parse(json);
            cache.Set(cacheKey, json, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(6), Size = 1 });
            return parsed;
        }
        throw new HttpRequestException("Too many Open Library redirects.");
    }

    private static string Text(JsonElement value, string key) =>
        value.TryGetProperty(key, out var field) && field.ValueKind == JsonValueKind.String ? field.GetString() ?? "" : "";
}
