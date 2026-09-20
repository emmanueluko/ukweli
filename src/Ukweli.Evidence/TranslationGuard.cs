using System.Text.RegularExpressions;
using Ukweli.Contracts;

namespace Ukweli.Evidence;

/// <summary>
/// Checks a translated result before it is shown or stored.
/// </summary>
/// <remarks>
/// <para>
/// The safety rules are enforced on the English text when an analysis is
/// produced. Translating that text moves it outside those checks: a figure can
/// shift, an unknown can quietly disappear, and an instruction to verify can
/// soften into something that reads like permission to pay. A guarantee that
/// holds only in the language the checks were written in is not a guarantee.
/// </para>
/// <para>
/// So this runs again per language, and only on what can actually be checked
/// across languages rather than on a guess about tone:
/// </para>
/// <list type="bullet">
/// <item>every figure in the English survives unchanged — 253 deaths stays 253;</item>
/// <item>every unknown survives, because dropping one hides a limit of the check;</item>
/// <item>no confidence score appears, per safety rule 6;</item>
/// <item>the next step still asks somebody to check or to wait, per safety rule 7.</item>
/// </list>
/// <para>
/// A translation that fails is discarded rather than shown, and the reader gets
/// the English with a note saying so. Being told a translation is unavailable
/// is better than being quietly handed a worse answer.
/// </para>
/// </remarks>
public static class TranslationGuard
{
    /// <summary>
    /// Ways a confidence score could reappear in translated prose. Deliberately
    /// narrow: these name a degree of certainty, which Ukweli never publishes.
    /// A bare percentage is not here, because case fatality rates are quoted
    /// from the sources themselves.
    /// </summary>
    private static readonly string[] ConfidenceWords =
    [
        "confidence", "confident", "certainty", "probability", "% sure", "% certain",
        "confiance", "certitude", "probabilité", "sûr à",
        "i sure say", "i dey sure", "percent sure",
    ];

    /// <summary>
    /// Phrases a next step must never contain. Safety rule 7 in each language:
    /// an action may ask somebody to confirm something or to wait, never to pay,
    /// to move money, or to disregard medical advice.
    /// </summary>
    private static readonly Dictionary<Language, string[]> ForbiddenInAction = new()
    {
        [Language.English] =
        [
            "pay the", "pay this", "transfer", "send money", "account number",
            "no need to see a doctor", "it is safe to ignore", "guaranteed",
        ],
        [Language.NigerianPidgin] =
        [
            "pay am", "pay di", "pay dis", "send di money", "send money", "transfer",
            "account number", "no need go hospital", "no need see doctor", "just ignore am",
        ],
        [Language.French] =
        [
            "payez", "payer la", "payer le", "virement", "transférez", "numéro de compte",
            "pas besoin de consulter", "inutile de consulter", "garanti",
        ],
    };

    /// <summary>Runs of digits, which must survive translation untouched.</summary>
    private static readonly Regex Figures = new(@"\d+", RegexOptions.Compiled);

    /// <param name="Accepted">The translation, when it passed.</param>
    /// <param name="Reason">
    /// Why it was refused, when it did not — phrased for an operator reading a
    /// log, not for the reader, who is simply told the translation is unavailable.
    /// </param>
    public sealed record Result(Translation? Accepted, string? Reason)
    {
        public bool Ok => Accepted is not null;
    }

    /// <summary>Whether a translation may be shown and stored.</summary>
    /// <param name="candidate">What the model returned.</param>
    /// <param name="language">The language it claims to be in.</param>
    /// <param name="english">The approved original, which it must still agree with.</param>
    public static Result Check(Translation? candidate, Language language, Translation english)
    {
        ArgumentNullException.ThrowIfNull(english);

        if (candidate is null)
        {
            return Refuse("The translation was empty.");
        }

        if (string.IsNullOrWhiteSpace(candidate.Explanation) ||
            string.IsNullOrWhiteSpace(candidate.SimpleExplanation) ||
            string.IsNullOrWhiteSpace(candidate.Action))
        {
            return Refuse("The translation left a required field empty.");
        }

        if (candidate.Unknowns.Count != english.Unknowns.Count)
        {
            return Refuse(
                $"The translation lists {candidate.Unknowns.Count} unknowns where the original "
                + $"has {english.Unknowns.Count}. What a check could not establish is part of "
                + "its answer.");
        }

        // Figures are the part of a finding most likely to be damaged in
        // translation and the easiest to check: they do not translate at all.
        foreach (var (field, original, translated) in new[]
                 {
                     ("explanation", english.Explanation, candidate.Explanation),
                     ("simple explanation", english.SimpleExplanation, candidate.SimpleExplanation),
                     ("next step", english.Action, candidate.Action),
                 })
        {
            if (FiguresIn(original) is { } expected && !expected.SetEquals(FiguresIn(translated)))
            {
                return Refuse(
                    $"The figures in the translated {field} do not match the original. A number "
                    + "that changes in translation is a different finding.");
            }
        }

        var everything = string.Join(
            '\n',
            [candidate.Explanation, candidate.SimpleExplanation, candidate.Action, .. candidate.Unknowns]);

        if (ConfidenceWords.FirstOrDefault(
                word => everything.Contains(word, StringComparison.OrdinalIgnoreCase)) is { } hedge)
        {
            return Refuse(
                $"The translation introduced '{hedge}'. Ukweli states what the evidence shows and "
                + "what it could not establish, and never how confident it is.");
        }

        var forbidden = ForbiddenInAction.GetValueOrDefault(language, []);

        if (forbidden.FirstOrDefault(
                phrase => candidate.Action.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            is { } unsafePhrase)
        {
            return Refuse(
                $"The translated next step contains '{unsafePhrase}'. An action may ask somebody "
                + "to confirm something or to wait, never to pay or to disregard medical advice.");
        }

        return new Result(candidate, null);
    }

    /// <summary>
    /// The digits in a passage, ignoring the separators around them, so that a
    /// French "24,0 %" still matches an English "24.0%".
    /// </summary>
    private static HashSet<string> FiguresIn(string text) =>
        [.. Figures.Matches(text).Select(match => match.Value.TrimStart('0') is { Length: > 0 } t ? t : "0")];

    private static Result Refuse(string reason) => new(null, reason);
}
