using System.Text;
using Ukweli.Contracts;

namespace Ukweli.Evidence.Ingestion;

/// <summary>
/// What an automatically ingested record must satisfy before it may be stored.
/// </summary>
/// <remarks>
/// <para>
/// This stands in for the human reader that safety rule 9 originally required.
/// The load-bearing check is <see cref="ExcerptAppearsInDocument"/>: the
/// excerpt must occur, as an exact run of words, in the text actually fetched
/// from the URL being cited. A machine can copy; it cannot compose. That makes
/// a fabricated quotation impossible rather than merely unlikely.
/// </para>
/// <para>
/// What it cannot do is judge whether a true sentence misleads once lifted out
/// of its surroundings. A person reading the page catches that and a substring
/// comparison never will, which is why every record this gate admits is stored
/// as <see cref="Curation.Automated"/> and stays distinguishable from one a
/// human chose.
/// </para>
/// </remarks>
public static class IngestGate
{
    /// <summary>Short enough to admit a one-line notice, long enough to carry meaning.</summary>
    public const int MinimumExcerptLength = 60;

    /// <summary>Beyond this it is not an excerpt, it is the document.</summary>
    public const int MaximumExcerptLength = 1200;

    /// <summary>A document dated after today is a parsing mistake, not a scoop.</summary>
    public static bool IsPlausiblePublicationDate(DateOnly? published, DateOnly today) =>
        published is { } date && date <= today && date >= today.AddYears(-10);

    /// <summary>
    /// Whether the excerpt genuinely occurs in the document.
    /// </summary>
    /// <remarks>
    /// Compared on collapsed whitespace and normalised quotation marks, because
    /// a PDF extractor introduces line breaks and curly quotes that carry no
    /// meaning. Nothing else is normalised: the words themselves must match, in
    /// order, exactly.
    /// </remarks>
    public static bool ExcerptAppearsInDocument(string? excerpt, string? documentText)
    {
        if (string.IsNullOrWhiteSpace(excerpt) || string.IsNullOrWhiteSpace(documentText))
        {
            return false;
        }

        return Flatten(documentText).Contains(Flatten(excerpt), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Collapses whitespace and unifies the quote and dash characters
    /// extractors mangle.
    /// </summary>
    /// <remarks>
    /// Public because ingestion must normalise a candidate excerpt exactly as
    /// this gate will. Two different normalisations would mean a passage
    /// genuinely lifted from a document failing the comparison against it.
    /// </remarks>
    public static string Flatten(string text)
    {
        var builder = new StringBuilder(text.Length);
        var lastWasSpace = false;

        foreach (var character in text)
        {
            var c = character switch
            {
                '‘' or '’' or 'ʼ' => '\'',
                '“' or '”' => '"',
                '‐' or '‑' or '‒' or '–' or '—' => '-',
                ' ' => ' ',
                _ => character,
            };

            if (char.IsWhiteSpace(c))
            {
                if (!lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                lastWasSpace = true;
                continue;
            }

            builder.Append(c);
            lastWasSpace = false;
        }

        return builder.ToString().Trim();
    }

    /// <param name="Candidate">The record proposed for the store.</param>
    /// <param name="DocumentText">The text fetched from its URL.</param>
    /// <param name="Today">Today, passed in so the gate stays testable.</param>
    public sealed record Submission(SourceRecord Candidate, string DocumentText, DateOnly Today);

    /// <summary>
    /// Checks a candidate. An empty result means it may be stored.
    /// </summary>
    public static IReadOnlyList<SourceProblem> Check(Submission submission)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var record = submission.Candidate;
        var problems = new List<SourceProblem>();
        var id = string.IsNullOrWhiteSpace(record.Id) ? SourceProblem.CorpusScope : record.Id;

        // Off the allowlist there is nothing further worth checking.
        var publisher = SourceRegistry.Match(record.Url);
        if (publisher is null)
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.NotOnAllowlist,
                $"'{record.Url}' is not on a publisher in the registry. An automated ingest may "
                + "only cite documents from hosts named there."));
            return problems;
        }

        // The issuer and the publisher's standing come from the registry, never
        // from the page: a document should not be able to describe itself as
        // official.
        if (!record.Issuer.Equals(publisher.Issuer, StringComparison.Ordinal))
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.IssuerMismatch,
                $"Issuer '{record.Issuer}' does not match the registry entry for "
                + $"{publisher.Host} ('{publisher.Issuer}')."));
        }

        if (record.SourceType != publisher.SourceType)
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.IssuerMismatch,
                $"{publisher.Host} is registered as {WireNames.ToWire(publisher.SourceType)}; "
                + $"this record claims {WireNames.ToWire(record.SourceType)}."));
        }

        if (!publisher.Topics.Contains(record.Topic))
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.TopicNotPublished,
                $"{publisher.Host} does not publish on {WireNames.ToWire(record.Topic)}."));
        }

        // The check that makes fabrication impossible rather than unlikely.
        if (!ExcerptAppearsInDocument(record.Excerpt, submission.DocumentText))
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.ExcerptNotInDocument,
                "The excerpt does not appear in the document fetched from that URL. An "
                + "automated ingest may quote a document, never summarise or compose one."));
        }

        var trimmed = record.Excerpt?.Trim() ?? string.Empty;
        if (trimmed.Length < MinimumExcerptLength)
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.ExcerptTooShort,
                $"The excerpt is {trimmed.Length} characters; at least {MinimumExcerptLength} "
                + "are needed for a passage that can carry a verdict."));
        }

        if (trimmed.Length > MaximumExcerptLength)
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.ExcerptTooLong,
                $"The excerpt is {trimmed.Length} characters. Beyond "
                + $"{MaximumExcerptLength} it is the document, not a passage from it."));
        }

        if (!IsPlausiblePublicationDate(record.PublishedAt, submission.Today))
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.ImplausibleDate,
                $"'{record.PublishedAt?.ToString("yyyy-MM-dd") ?? "none"}' is not a plausible "
                + "publication date. A date in the future is a parsing mistake."));
        }

        if (record.Curation != Curation.Automated)
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.WrongCuration,
                "A record created by an ingest must be marked automated, so that what nobody "
                + "read stays distinguishable from what somebody did."));
        }

        return problems;
    }
}
