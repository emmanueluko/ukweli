using System.Text.RegularExpressions;

namespace Ukweli.Evidence;

/// <summary>
/// Decides whether the curated store is fit to be seeded. Pure: it is handed
/// records and returns problems, and never touches the network or a database.
/// The URL reachability check lives outside this project, because it must.
/// </summary>
public static partial class SourceValidator
{
    /// <summary>
    /// Any field beginning with this marks a record a human has not curated yet.
    /// </summary>
    public const string PlaceholderMarker = "PLACEHOLDER";

    /// <summary>
    /// Short enough to admit a one-line notice, long enough to reject a stray
    /// fragment that could not support a verdict on its own.
    /// </summary>
    public const int MinimumExcerptLength = 40;

    [GeneratedRegex(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern { get; }

    /// <summary>
    /// True when any curator-supplied field still carries the placeholder
    /// marker, whatever the <c>placeholder</c> flag claims. A record cannot
    /// escape the gate by flipping the flag while leaving the text unwritten.
    /// </summary>
    public static bool LooksUncurated(SourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return record.Placeholder
            || ContainsMarker(record.Id)
            || ContainsMarker(record.Title)
            || ContainsMarker(record.Url)
            || ContainsMarker(record.Excerpt)
            || ContainsMarker(record.Issuer);
    }

    private static bool ContainsMarker(string? value) =>
        value is not null && value.Contains(PlaceholderMarker, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Validates the whole corpus. An empty result means the store is fit to
    /// seed; every problem returned is a reason it is not.
    /// </summary>
    public static IReadOnlyList<SourceProblem> Validate(IReadOnlyList<SourceRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var problems = new List<SourceProblem>();

        if (records.Count == 0)
        {
            problems.Add(new SourceProblem(
                SourceProblem.CorpusScope,
                SourceProblemCodes.CorpusEmpty,
                "The curated store is empty. Ukweli cannot check a claim against nothing."));
            return problems;
        }

        if (records.Count > SourceCorpus.MaxRecords)
        {
            problems.Add(new SourceProblem(
                SourceProblem.CorpusScope,
                SourceProblemCodes.CorpusTooLarge,
                $"The curated store holds {records.Count} records; the cap is {SourceCorpus.MaxRecords}. "
                + "Retrieval is keyword scoring over this list, not search."));
        }

        problems.AddRange(FindDuplicateIds(records));

        foreach (var record in records)
        {
            problems.AddRange(ValidateRecord(record));
        }

        return problems;
    }

    private static IEnumerable<SourceProblem> FindDuplicateIds(IReadOnlyList<SourceRecord> records)
    {
        return records
            .GroupBy(record => record.Id, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => new SourceProblem(
                group.Key,
                SourceProblemCodes.DuplicateId,
                $"Id '{group.Key}' is used by {group.Count()} records. Ids identify a source in "
                + "every API response and must be unique."));
    }

    /// <summary>Validates one record in isolation.</summary>
    public static IReadOnlyList<SourceProblem> ValidateRecord(SourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        var problems = new List<SourceProblem>();
        var id = string.IsNullOrWhiteSpace(record.Id) ? SourceProblem.CorpusScope : record.Id;

        // Safety rule 9. Reported first, and on its own, because every other
        // complaint about an uncurated record is noise.
        if (LooksUncurated(record))
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.Placeholder,
                "Not curated yet. A human must supply the specific document URL, its title, "
                + "its dates and a verbatim excerpt, then set \"placeholder\": false. "
                + "See src/Ukweli.Evidence/data/README.md."));
            return problems;
        }

        AddMissingFieldProblems(record, id, problems);

        if (!string.IsNullOrWhiteSpace(record.Id) && !SlugPattern.IsMatch(record.Id))
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.InvalidId,
                $"Id '{record.Id}' is not a readable slug. Use lowercase words joined by hyphens, "
                + "for example 'ncdc-lassa-sitrep-w33-2026'."));
        }

        AddUrlProblems(record, id, problems);
        AddDateProblems(record, id, problems);

        if (!string.IsNullOrWhiteSpace(record.Excerpt) &&
            record.Excerpt.Trim().Length < MinimumExcerptLength)
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.ExcerptTooShort,
                $"The excerpt is {record.Excerpt.Trim().Length} characters; at least "
                + $"{MinimumExcerptLength} are needed for a passage that can carry a verdict."));
        }

        return problems;
    }

    private static void AddMissingFieldProblems(
        SourceRecord record, string id, List<SourceProblem> problems)
    {
        var required = new (string Field, string? Value)[]
        {
            ("id", record.Id),
            ("issuer", record.Issuer),
            ("title", record.Title),
            ("url", record.Url),
            ("excerpt", record.Excerpt),
        };

        foreach (var (field, value) in required)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                problems.Add(new SourceProblem(
                    id,
                    SourceProblemCodes.MissingField,
                    $"'{field}' is required and is blank."));
            }
        }
    }

    private static void AddUrlProblems(SourceRecord record, string id, List<SourceProblem> problems)
    {
        if (string.IsNullOrWhiteSpace(record.Url))
        {
            return;
        }

        if (!IsHttpUrl(record.Url))
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.InvalidUrl,
                $"'url' must be an absolute http or https address; found '{record.Url}'."));
        }
        else if (record.CollectionUrl is not null &&
                 UrlsMatch(record.Url, record.CollectionUrl))
        {
            // The index is where a curator looked, not what the claim is checked
            // against. A verdict citing an index cites nothing in particular.
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.UrlIsIndexPage,
                "'url' is the same as 'collectionUrl'. It must point at the specific "
                + "document, never the index it was found on."));
        }

        if (record.CollectionUrl is not null &&
            !string.IsNullOrWhiteSpace(record.CollectionUrl) &&
            !IsHttpUrl(record.CollectionUrl))
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.InvalidUrl,
                $"'collectionUrl' must be an absolute http or https address; found '{record.CollectionUrl}'."));
        }
    }

    private static void AddDateProblems(SourceRecord record, string id, List<SourceProblem> problems)
    {
        if (record.PublishedAt is null)
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.MissingDate,
                "'publishedAt' is required: users are told when a source was published."));
        }

        if (record.CheckedAt is null)
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.MissingDate,
                "'checkedAt' is required: it is the 'as checked on' caveat shown with every verdict."));
        }

        if (record.PublishedAt is { } published &&
            record.CheckedAt is { } checkedAt &&
            checkedAt < published)
        {
            problems.Add(new SourceProblem(
                id,
                SourceProblemCodes.DatesOutOfOrder,
                $"'checkedAt' ({checkedAt:yyyy-MM-dd}) is before 'publishedAt' ({published:yyyy-MM-dd})."));
        }
    }

    public static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool UrlsMatch(string left, string right) =>
        string.Equals(left.TrimEnd('/'), right.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
}
