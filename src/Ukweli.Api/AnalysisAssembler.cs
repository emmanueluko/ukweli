using Ukweli.Contracts;
using Ukweli.Data;
using Ukweli.Data.Entities;
using Ukweli.Evidence;

namespace Ukweli.Api;

/// <summary>
/// Turns a stored analysis and its sources into the response clients see.
/// </summary>
public sealed class AnalysisAssembler(
    SourceRepository sources,
    TranslationService translations,
    UkweliOptions options)
{
    /// <param name="language">
    /// The language to read the result in. Only the prose changes: the verdict,
    /// the sources, the dates and the relations are the same in every language,
    /// and a source excerpt is never translated, because a translated quotation
    /// is no longer a quotation.
    /// </param>
    /// <param name="translate">
    /// False to render from stored translations only, never calling the model.
    /// </param>
    public async Task<AnalysisResponse> AssembleAsync(
        ClaimAnalysis analysis,
        Language language = Language.English,
        bool translate = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var rendering = await translations.RenderAsync(
            analysis, language, translate, cancellationToken);

        var resolved = await sources.ResolveAsync(analysis.SourceIds, cancellationToken);

        var cited = resolved
            .Select(source => new AnalysisSourceResponse(
                Id: source.Id,
                Issuer: source.Issuer,
                Title: source.Title,
                Url: source.Url,
                Excerpt: source.Excerpt,
                PublishedAt: source.PublishedAt,
                CheckedAt: source.CheckedAt,
                Relation: analysis.SourceRelations.GetValueOrDefault(
                    source.Id, SourceRelation.Context),
                SourceType: source.SourceType,
                Placeholder: source.Placeholder))
            .ToList();

        var checkedOn = CheckedOn(resolved);

        return new AnalysisResponse(
            Id: analysis.Id,
            NormalizedClaim: analysis.NormalizedClaim,
            Jurisdiction: analysis.Jurisdiction,
            Status: analysis.Status,
            Explanation: rendering.Text.Explanation,
            SimpleExplanation: rendering.Text.SimpleExplanation,
            Unknowns: rendering.Text.Unknowns,
            Action: rendering.Text.Action,
            ActionIsGeneric: analysis.ActionIsGeneric,
            Sources: cited,
            CheckedOn: checkedOn,
            ShareText: ShareText.Build(
                analysis.Status,
                checkedOn,
                PrimarySourceUrl(resolved, analysis),
                options.AppUrl,
                analysis.Id,
                rendering.Language),
            Language: rendering.Language,
            AvailableLanguages: Languages.All,
            TranslationNote: rendering.Note);
    }

    /// <summary>
    /// The most recent date a human checked any cited source. Null when nothing
    /// was cited, which is the honest answer for an insufficient-evidence
    /// verdict: no source was checked, so no date can be claimed.
    /// </summary>
    private static DateOnly? CheckedOn(IReadOnlyList<Source> sources) =>
        sources.Select(source => source.CheckedAt).OfType<DateOnly>().DefaultIfEmpty().Max()
            is { } date && date != default
            ? date
            : null;

    /// <summary>
    /// The document the share text points at: the first cited primary source
    /// that actually bears on the claim. A secondary source is never offered as
    /// the source of a verdict, and a context-only citation is not what the
    /// verdict rests on.
    /// </summary>
    private static string? PrimarySourceUrl(IReadOnlyList<Source> sources, ClaimAnalysis analysis)
    {
        var bearing = sources
            .Where(source => source.SourceType == SourceType.PrimaryOfficial)
            .FirstOrDefault(source =>
                analysis.SourceRelations.GetValueOrDefault(source.Id, SourceRelation.Context)
                    is SourceRelation.Supports or SourceRelation.Conflicts);

        return bearing?.Url;
    }
}
