namespace Ukweli.Contracts;

/// <summary>
/// A curated source as <c>GET /api/sources/{id}</c> returns it.
/// </summary>
/// <param name="Id">The readable slug that identifies this source everywhere.</param>
/// <param name="Issuer">The body that published the document.</param>
/// <param name="Title">The document's own title.</param>
/// <param name="SourceType">Primary official, or trusted secondary.</param>
/// <param name="Jurisdiction">Where it applies.</param>
/// <param name="Topic">The civic area it covers.</param>
/// <param name="PublishedAt">The date on the document.</param>
/// <param name="CheckedAt">The date a human last read it.</param>
/// <param name="Url">The specific document, never an index page.</param>
/// <param name="CollectionUrl">The index it was found on, if recorded.</param>
/// <param name="Excerpt">A verbatim passage. Never paraphrased.</param>
/// <param name="Placeholder">True while the record is still uncurated.</param>
/// <param name="ExternalContentMayChange">
/// Always true, and stated rather than implied: Ukweli quotes what a document
/// said when it was checked. The page behind <paramref name="Url"/> can be
/// edited, moved or withdrawn at any time, and the excerpt here is the evidence
/// — not whatever that address serves today.
/// </param>
public sealed record SourceResponse(
    string Id,
    string Issuer,
    string Title,
    SourceType SourceType,
    Jurisdiction Jurisdiction,
    Topic Topic,
    DateOnly? PublishedAt,
    DateOnly? CheckedAt,
    string Url,
    string? CollectionUrl,
    string Excerpt,
    bool Placeholder,
    bool ExternalContentMayChange = true);
