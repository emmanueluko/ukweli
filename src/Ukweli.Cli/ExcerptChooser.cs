using System.Globalization;
using System.Text.RegularExpressions;
using Ukweli.Contracts;
using Ukweli.Evidence;
using Ukweli.Evidence.Ingestion;

namespace Ukweli.Cli;

/// <summary>
/// Picks the passage of a document worth quoting, and the date it carries.
/// </summary>
/// <remarks>
/// Everything here works by cutting, never by composing. The excerpt returned
/// is a literal run of characters from the extracted text, so it satisfies the
/// verbatim gate by construction rather than by luck.
///
/// That makes the gate look redundant against this implementation, and it is
/// not: it is the invariant that stops a later change — an LLM asked to
/// "summarise the key passage", say — from quietly turning a quotation into a
/// paraphrase. The check costs nothing and it is the reason the store can be
/// trusted without anyone reading it.
/// </remarks>
public static partial class ExcerptChooser
{
    [GeneratedRegex(@"(?<=[.!?])\s+", RegexOptions.CultureInvariant)]
    private static partial Regex SentenceBreak { get; }

    [GeneratedRegex(
        @"\b(\d{1,2})(?:st|nd|rd|th)?\s+(January|February|March|April|May|June|July|August|September|October|November|December)\s+(\d{4})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LongDate { get; }

    [GeneratedRegex(@"\b(\d{4})-(\d{2})-(\d{2})\b", RegexOptions.CultureInvariant)]
    private static partial Regex IsoDate { get; }

    /// <summary>
    /// The most topical run of consecutive sentences that fits the length
    /// bounds, or null when nothing in the document is on topic.
    /// </summary>
    public static string? Choose(string documentText, Topic topic)
    {
        ArgumentNullException.ThrowIfNull(documentText);

        var sentences = SentenceBreak
            .Split(IngestGate.Flatten(documentText))
            .Select(sentence => sentence.Trim())
            .Where(sentence => sentence.Length > 25)
            .ToList();

        if (sentences.Count == 0)
        {
            return null;
        }

        var terms = Retriever.TopicTerms(topic);
        string? best = null;
        var bestScore = 0;

        // Windows of consecutive sentences, so the excerpt reads as it does in
        // the document rather than as a sentence plucked out of the middle.
        for (var start = 0; start < sentences.Count; start++)
        {
            var window = new List<string>();

            for (var length = 0; length < 4 && start + length < sentences.Count; length++)
            {
                window.Add(sentences[start + length]);
                var candidate = string.Join(" ", window);

                if (candidate.Length < IngestGate.MinimumExcerptLength)
                {
                    continue;
                }

                if (candidate.Length > IngestGate.MaximumExcerptLength)
                {
                    break;
                }

                var words = Retriever.Tokenise(candidate);
                var score = terms.Count(words.Contains);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
        }

        // A document that never mentions the topic is not evidence about it.
        return bestScore >= 2 ? best : null;
    }

    /// <summary>
    /// The publication date the document states.
    /// </summary>
    /// <remarks>
    /// Read from the text, never inferred from when the crawl happened. A
    /// document with no date it is willing to state does not get one invented
    /// for it — the gate then rejects the record, which is the right outcome:
    /// every verdict shows its source's date, and a wrong date is worse than
    /// no source.
    /// </remarks>
    public static DateOnly? FindPublicationDate(string documentText, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(documentText);

        // Only the opening of a document, where a dateline lives. Later dates
        // are usually the subject matter, not the publication.
        var head = documentText.Length > 4000 ? documentText[..4000] : documentText;
        var found = new List<DateOnly>();

        foreach (Match match in LongDate.Matches(head))
        {
            if (DateTime.TryParseExact(
                    $"{match.Groups[1].Value} {match.Groups[2].Value} {match.Groups[3].Value}",
                    "d MMMM yyyy",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                found.Add(DateOnly.FromDateTime(parsed));
            }
        }

        foreach (Match match in IsoDate.Matches(head))
        {
            if (DateOnly.TryParse(match.Value, CultureInfo.InvariantCulture, out var parsed))
            {
                found.Add(parsed);
            }
        }

        return found
            .Where(date => IngestGate.IsPlausiblePublicationDate(date, today))
            .OrderByDescending(date => date)
            .Select(date => (DateOnly?)date)
            .FirstOrDefault();
    }

    /// <summary>The document's title, taken from its first substantial line.</summary>
    public static string? FindTitle(string documentText)
    {
        ArgumentNullException.ThrowIfNull(documentText);

        var line = documentText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(candidate => candidate.Length is > 15 and < 200);

        return line is null ? null : IngestGate.Flatten(line);
    }

    /// <summary>A readable, stable id: the host, the topic, and the date.</summary>
    public static string BuildId(string host, Topic topic, DateOnly published, string url)
    {
        var organisation = host.Split('.')[0].ToLowerInvariant();
        var subject = WireNames.ToWire(topic).Replace('_', '-');

        // The hash keeps two documents published the same day by the same body
        // from colliding, without putting an opaque URL in the id.
        var fingerprint = Math.Abs(url.GetHashCode(StringComparison.Ordinal)) % 100000;

        return $"{organisation}-{subject}-{published:yyyyMMdd}-{fingerprint}";
    }
}
