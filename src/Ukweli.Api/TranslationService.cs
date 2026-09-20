using Ukweli.Contracts;
using Ukweli.Data;
using Ukweli.Data.Entities;
using Ukweli.Evidence;

namespace Ukweli.Api;

/// <summary>
/// Renders a finished analysis in the language the reader asked for.
/// </summary>
/// <remarks>
/// <para>
/// Translation happens after the verdict, never as part of it. The status, the
/// sources, the dates and the relations are identical in every language;
/// switching language changes how the answer reads and never what it says. A
/// source excerpt is never translated at all — Ukweli's promise is that it
/// quotes the document, and a translated quotation is no longer a quotation.
/// </para>
/// <para>
/// A language is produced once and then stored on the row. That makes switching
/// instant after the first time, keeps a shared link reading the same for
/// everyone who opens it, and means a reader is never charged the wait twice
/// for the same words.
/// </para>
/// </remarks>
public sealed class TranslationService(
    IAiProvider ai,
    AnalysisRepository analyses,
    SeedStore seeds,
    ILogger<TranslationService> logger)
{
    /// <param name="Text">The result as it should be read.</param>
    /// <param name="Language">
    /// The language actually returned, which is English whenever the requested
    /// one could not be produced.
    /// </param>
    /// <param name="Note">
    /// Set only when the reader asked for a language they did not get, so the
    /// interface can say so rather than pretending the English is a translation.
    /// </param>
    public sealed record Rendering(Translation Text, Language Language, string? Note);

    /// <summary>Renders <paramref name="analysis"/> in <paramref name="language"/>.</summary>
    /// <param name="generate">
    /// False to use only what is already stored. A list of somebody's past
    /// checks would otherwise cost one model call per row on every page load,
    /// so it takes the translations it has and shows English for the rest.
    /// </param>
    public async Task<Rendering> RenderAsync(
        ClaimAnalysis analysis,
        Language language,
        bool generate = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var english = new Translation(
            analysis.Explanation, analysis.SimpleExplanation, analysis.Unknowns, analysis.Action);

        if (language == Language.English)
        {
            return new Rendering(english, Language.English, null);
        }

        var code = WireNames.ToWire(language);

        // Already rendered, either on a previous request or by a curator.
        if (analysis.Translations.TryGetValue(code, out var stored))
        {
            return new Rendering(stored, language, null);
        }

        // A seeded claim never reaches the model, in any language.
        if (analysis.SeedId is { } seedId && HandWritten(seedId, code) is { } curated)
        {
            var checkedCuration = TranslationGuard.Check(curated, language, english);

            if (!checkedCuration.Ok)
            {
                logger.TranslationRefused(analysis.Id, code, checkedCuration.Reason!);
                return Fallback(english, language);
            }

            await analyses.SaveTranslationAsync(analysis.Id, code, curated, cancellationToken);
            return new Rendering(curated, language, null);
        }

        if (!generate)
        {
            return Fallback(english, language);
        }

        Translation? drafted;
        try
        {
            drafted = await ai.TranslateAsync(english, language, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A translation that cannot be produced is not a failed check. The
            // finding stands; only this rendering of it is unavailable, so the
            // reader gets the English rather than a 503.
            logger.TranslationUnavailable(analysis.Id, code, ex.Message);
            return Fallback(english, language);
        }

        var verified = TranslationGuard.Check(drafted, language, english);

        if (!verified.Ok)
        {
            logger.TranslationRefused(analysis.Id, code, verified.Reason!);
            return Fallback(english, language);
        }

        await analyses.SaveTranslationAsync(analysis.Id, code, verified.Accepted!, cancellationToken);

        return new Rendering(verified.Accepted!, language, null);
    }

    /// <summary>The curator's own rendering of a seeded claim, if there is one.</summary>
    private Translation? HandWritten(string seedId, string code) =>
        seeds.Records.FirstOrDefault(seed => seed.Id == seedId)
            ?.Translations.GetValueOrDefault(code);

    /// <summary>
    /// English, and a plain statement of why. Saying the translation is
    /// unavailable is honest; showing English while labelled as Pidgin is not.
    /// </summary>
    private static Rendering Fallback(Translation english, Language requested) =>
        new(
            english,
            Language.English,
            $"This result is not available in {Languages.EndonymOf(requested)} yet, so it is "
            + "shown in English. The verdict and the sources are the same either way.");
}
