using Ukweli.Contracts;

namespace Ukweli.Data.Entities;

/// <summary>
/// A curated source as stored in Postgres, mapped to the <c>sources</c> table.
/// </summary>
/// <remarks>
/// The shape mirrors <c>Ukweli.Evidence.SourceRecord</c> deliberately rather
/// than sharing a type: the evidence library must stay free of any database
/// dependency, so the corpus record and the persisted row are separate types
/// that the seed command translates between.
/// </remarks>
public class Source
{
    /// <summary>Readable slug, e.g. <c>ncdc-lassa-sitrep-w33-2026</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public SourceType SourceType { get; set; }

    public Jurisdiction Jurisdiction { get; set; }

    public Topic Topic { get; set; }

    /// <summary>
    /// Nullable only so an uncurated placeholder can be stored without an
    /// invented date. <c>verify-sources</c> rejects a curated record that
    /// leaves it null.
    /// </summary>
    public DateOnly? PublishedAt { get; set; }

    /// <summary>The "as checked on" date shown with every verdict citing this source.</summary>
    public DateOnly? CheckedAt { get; set; }

    /// <summary>The specific document. Never an index page.</summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>The index the document was found on.</summary>
    public string? CollectionUrl { get; set; }

    /// <summary>A verbatim passage from the document.</summary>
    public string Excerpt { get; set; } = string.Empty;

    /// <summary>True until a human has curated the record (safety rule 9).</summary>
    public bool Placeholder { get; set; }

    /// <summary>False retires the record from retrieval without deleting it.</summary>
    public bool Active { get; set; } = true;
}
