using System.Text.Json.Serialization;
using Ukweli.Contracts;

namespace Ukweli.Evidence;

/// <summary>
/// One record in the curated store, exactly as <c>data/sources.json</c> spells
/// it.
/// </summary>
/// <remarks>
/// Dates are nullable only so an uncurated placeholder can be represented
/// honestly rather than carrying an invented date. A record with
/// <see cref="Placeholder"/> false and a missing date fails validation.
/// </remarks>
public sealed record SourceRecord
{
    /// <summary>Readable slug, unique across the corpus, e.g. <c>ncdc-lassa-sitrep-w33-2026</c>.</summary>
    public required string Id { get; init; }

    /// <summary>The body that published the document, as it names itself.</summary>
    public required string Issuer { get; init; }

    /// <summary>The document's own title.</summary>
    public required string Title { get; init; }

    public required SourceType SourceType { get; init; }

    public required Jurisdiction Jurisdiction { get; init; }

    public required Topic Topic { get; init; }

    /// <summary>The date printed on the document.</summary>
    public DateOnly? PublishedAt { get; init; }

    /// <summary>The date a human last read it — the "as checked on" caveat users see.</summary>
    public DateOnly? CheckedAt { get; init; }

    /// <summary>The specific document. Never an index page.</summary>
    public required string Url { get; init; }

    /// <summary>The index the document was found on, kept so it can be re-checked.</summary>
    public string? CollectionUrl { get; init; }

    /// <summary>A verbatim passage. Never paraphrased, never stitched together.</summary>
    public required string Excerpt { get; init; }

    /// <summary>
    /// True until a human has curated the record. Safety rule 9: a placeholder
    /// must fail <c>verify-sources</c>, so an uncurated corpus cannot reach a demo.
    /// </summary>
    public bool Placeholder { get; init; }

    /// <summary>False retires a record from retrieval without deleting it.</summary>
    public bool Active { get; init; } = true;

    /// <summary>
    /// Whether this record can carry a claim on its own. Safety rules 3 and 5:
    /// only a primary official source can support or contradict, and a
    /// secondary source never overrides a primary.
    /// </summary>
    [JsonIgnore]
    public bool IsPrimary => SourceType == SourceType.PrimaryOfficial;
}
