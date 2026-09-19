namespace Ukweli.Evidence;

/// <summary>Why a record in the curated store is not fit to be seeded.</summary>
/// <param name="SourceId">The offending record's id, or <c>(corpus)</c> for a whole-file problem.</param>
/// <param name="Code">A stable code from <see cref="SourceProblemCodes"/>.</param>
/// <param name="Message">A sentence a curator can act on.</param>
public sealed record SourceProblem(string SourceId, string Code, string Message)
{
    public const string CorpusScope = "(corpus)";

    public override string ToString() => $"[{Code}] {SourceId}: {Message}";
}

public static class SourceProblemCodes
{
    /// <summary>Safety rule 9 — an uncurated record must block the corpus.</summary>
    public const string Placeholder = "placeholder";
    public const string DuplicateId = "duplicate_id";
    public const string MissingField = "missing_field";
    public const string InvalidId = "invalid_id";
    public const string InvalidUrl = "invalid_url";
    public const string UrlIsIndexPage = "url_is_index_page";
    public const string MissingDate = "missing_date";
    public const string DatesOutOfOrder = "dates_out_of_order";
    public const string ExcerptTooShort = "excerpt_too_short";
    public const string CorpusTooLarge = "corpus_too_large";
    public const string CorpusEmpty = "corpus_empty";
    public const string UnreachableUrl = "unreachable_url";
}
