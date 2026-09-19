namespace Ukweli.Evidence;

/// <summary>
/// Normalisation applied to a record as it is read from the curated store.
/// </summary>
/// <remarks>
/// Trimming happens here, once, rather than at every point of use. It never
/// changes an excerpt's wording — only surrounding whitespace — because an
/// excerpt is quoted verbatim by definition.
/// </remarks>
public static class SourceMapping
{
    public static SourceRecord Normalise(SourceRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return record with
        {
            Id = record.Id.Trim(),
            Issuer = record.Issuer.Trim(),
            Title = record.Title.Trim(),
            Url = record.Url.Trim(),
            CollectionUrl = string.IsNullOrWhiteSpace(record.CollectionUrl)
                ? null
                : record.CollectionUrl.Trim(),
            Excerpt = record.Excerpt.Trim(),
        };
    }

    public static IReadOnlyList<SourceRecord> Normalise(IReadOnlyList<SourceRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return [.. records.Select(Normalise)];
    }
}
