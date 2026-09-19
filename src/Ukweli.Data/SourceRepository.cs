using Microsoft.EntityFrameworkCore;
using Ukweli.Contracts;
using Ukweli.Data.Entities;

namespace Ukweli.Data;

/// <summary>Read access to the curated store.</summary>
public sealed class SourceRepository(UkweliDbContext db)
{
    /// <summary>
    /// Finds one source by id, whether or not it is active — a retired source
    /// must still resolve, because analyses stored earlier cite it and those
    /// results have to stay readable.
    /// </summary>
    public async Task<Source?> FindAsync(string id, CancellationToken cancellationToken = default) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : await db.Sources
                .AsNoTracking()
                .FirstOrDefaultAsync(source => source.Id == id.Trim(), cancellationToken);

    /// <summary>Every active source, for retrieval. The corpus is small by design.</summary>
    public async Task<IReadOnlyList<Source>> ListActiveAsync(
        CancellationToken cancellationToken = default) =>
        await db.Sources
            .AsNoTracking()
            .Where(source => source.Active)
            .OrderBy(source => source.Id)
            .ToListAsync(cancellationToken);

    /// <summary>Resolves several ids at once, preserving the order asked for.</summary>
    public async Task<IReadOnlyList<Source>> ResolveAsync(
        IReadOnlyList<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        var found = await db.Sources
            .AsNoTracking()
            .Where(source => ids.Contains(source.Id))
            .ToDictionaryAsync(source => source.Id, cancellationToken);

        return [.. ids.Select(id => found.GetValueOrDefault(id)).OfType<Source>()];
    }

    public static SourceResponse ToResponse(Source source)
    {
        ArgumentNullException.ThrowIfNull(source);

        return new SourceResponse(
            Id: source.Id,
            Issuer: source.Issuer,
            Title: source.Title,
            SourceType: source.SourceType,
            Jurisdiction: source.Jurisdiction,
            Topic: source.Topic,
            PublishedAt: source.PublishedAt,
            CheckedAt: source.CheckedAt,
            Url: source.Url,
            CollectionUrl: source.CollectionUrl,
            Excerpt: source.Excerpt,
            Placeholder: source.Placeholder);
    }
}
