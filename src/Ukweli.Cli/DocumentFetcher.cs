using HtmlAgilityPack;
using UglyToad.PdfPig;

namespace Ukweli.Cli;

/// <summary>
/// Fetches a document and returns its text, whatever it is made of.
/// </summary>
/// <remarks>
/// The text this returns is what the verbatim gate compares an excerpt
/// against, so it has to be the document's real words. Nothing here rewrites,
/// summarises or corrects anything — it only removes markup and layout.
/// </remarks>
public sealed class DocumentFetcher(HttpClient http)
{
    /// <summary>Government sites are often slow; this is not an interactive path.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(45);

    /// <summary>A document larger than this is not a notice worth quoting.</summary>
    private const int MaxBytes = 25 * 1024 * 1024;

    public sealed record Document(string Url, string Text, string ContentType);

    public async Task<Document?> FetchAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var contentType = response.Content.Headers.ContentType?.MediaType ?? "text/html";

            if (response.Content.Headers.ContentLength is > MaxBytes)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length > MaxBytes)
            {
                return null;
            }

            var text = contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase)
                ? ExtractPdf(bytes)
                : ExtractHtml(System.Text.Encoding.UTF8.GetString(bytes));

            return string.IsNullOrWhiteSpace(text) ? null : new Document(url, text, contentType);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            return null;
        }
    }

    internal static string ExtractPdf(byte[] bytes)
    {
        try
        {
            using var pdf = PdfDocument.Open(bytes);
            return string.Join(
                "\n",
                pdf.GetPages().Select(page => page.Text));
        }
        catch (Exception)
        {
            // A PDF that will not parse yields no text, and no text means the
            // verbatim gate rejects anything claiming to quote it.
            return string.Empty;
        }
    }

    internal static string ExtractHtml(string html)
    {
        var document = new HtmlDocument();
        document.LoadHtml(html);

        // Script and style contents are not the document's words.
        var noise = document.DocumentNode
            .SelectNodes("//script|//style|//nav|//header|//footer|//noscript");

        if (noise is not null)
        {
            foreach (var node in noise)
            {
                node.Remove();
            }
        }

        var body = document.DocumentNode.SelectSingleNode("//body") ?? document.DocumentNode;
        return HtmlEntity.DeEntitize(body.InnerText) ?? string.Empty;
    }

    /// <summary>Finds the document links on an index page, within the same publisher.</summary>
    public async Task<IReadOnlyList<string>> DiscoverAsync(
        string collectionUrl, string host, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.GetAsync(collectionUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return [];
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken);
            var document = new HtmlDocument();
            document.LoadHtml(html);

            var anchors = document.DocumentNode.SelectNodes("//a[@href]");
            if (anchors is null)
            {
                return [];
            }

            var baseUri = new Uri(collectionUrl);
            var found = new List<string>();

            foreach (var anchor in anchors)
            {
                var href = anchor.GetAttributeValue("href", null);
                if (string.IsNullOrWhiteSpace(href) || href.StartsWith('#'))
                {
                    continue;
                }

                if (!Uri.TryCreate(baseUri, href, out var absolute))
                {
                    continue;
                }

                // Stay within the publisher, and never treat the index itself
                // as one of its own documents.
                if (!absolute.Host.EndsWith(host, StringComparison.OrdinalIgnoreCase) ||
                    absolute.ToString().TrimEnd('/') == collectionUrl.TrimEnd('/'))
                {
                    continue;
                }

                found.Add(absolute.ToString());
            }

            return [.. found.Distinct(StringComparer.OrdinalIgnoreCase)];
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return [];
        }
    }
}
