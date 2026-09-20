using Microsoft.EntityFrameworkCore;
using Ukweli.Contracts;
using Ukweli.Data.Entities;

namespace Ukweli.Data;

/// <summary>Read and write access to stored analyses.</summary>
public sealed class AnalysisRepository(UkweliDbContext db)
{
    public async Task<ClaimAnalysis?> FindAsync(
        string id, CancellationToken cancellationToken = default) =>
        // Rejecting a malformed id before touching the database keeps a
        // scanning client from turning /r/{id} into free query load.
        AnalysisId.IsWellFormed(id?.Trim())
            ? await db.ClaimAnalyses
                .AsNoTracking()
                .FirstOrDefaultAsync(analysis => analysis.Id == id!.Trim(), cancellationToken)
            : null;

    /// <summary>
    /// Returns the stored analysis for a seeded claim, or null if it has not
    /// been run yet. Seeds are persisted on first use rather than up front, so
    /// a seeded result has a real id and a real <c>/r/{id}</c> link like any other.
    /// </summary>
    public async Task<ClaimAnalysis?> FindBySeedAsync(
        string seedId, string? userId, CancellationToken cancellationToken = default) =>
        // Scoped to the caller. Without this, the first anonymous run of a
        // seeded claim created the only row that would ever exist for it, and
        // every signed-in user who checked the same claim afterwards was handed
        // that anonymous row — so their check never appeared in their history.
        await db.ClaimAnalyses
            .AsNoTracking()
            .Where(analysis => analysis.SeedId == seedId && analysis.UserId == userId)
            .OrderBy(analysis => analysis.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ClaimAnalysis> AddAsync(
        ClaimAnalysis analysis, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        db.ClaimAnalyses.Add(analysis);
        await db.SaveChangesAsync(cancellationToken);
        return analysis;
    }

    /// <summary>
    /// Stores a translation against an analysis, so the next reader of the same
    /// link in the same language gets it immediately and gets the same words.
    /// </summary>
    /// <remarks>
    /// Write-once: a language already present is left alone. Two readers asking
    /// for Pidgin at the same moment would otherwise each store their own
    /// rendering, and the second would silently change the text under the first.
    /// </remarks>
    public async Task SaveTranslationAsync(
        string analysisId,
        string language,
        Translation translation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(translation);

        var analysis = await db.ClaimAnalyses
            .FirstOrDefaultAsync(row => row.Id == analysisId, cancellationToken);

        if (analysis is null || analysis.Translations.ContainsKey(language))
        {
            return;
        }

        // Replaced rather than mutated: the jsonb comparer tracks the dictionary
        // by value, and an in-place add on the same instance is not seen as a change.
        analysis.Translations = new Dictionary<string, Translation>(analysis.Translations)
        {
            [language] = translation,
        };

        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A caller's own analyses, newest first (Phase 4).</summary>
    public async Task<IReadOnlyList<ClaimAnalysis>> ListForUserAsync(
        string userId, int limit = 50, CancellationToken cancellationToken = default) =>
        await db.ClaimAnalyses
            .AsNoTracking()
            .Where(analysis => analysis.UserId == userId)
            .OrderByDescending(analysis => analysis.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
}
