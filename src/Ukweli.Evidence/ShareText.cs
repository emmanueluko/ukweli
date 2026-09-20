using System.Globalization;
using Ukweli.Contracts;

namespace Ukweli.Evidence;

/// <summary>
/// Builds the short summary a user forwards.
/// </summary>
/// <remarks>
/// <para>
/// This is the part of a result most likely to travel further than the page it
/// came from, so it is built here, server-side, and never assembled by a client
/// or a model. That holds in every language: the wording below is fixed and
/// hand-written, so the thing most likely to be forwarded is never something a
/// model produced.
/// </para>
/// <para>
/// It deliberately carries none of the user's own words — not the pasted text
/// and not the normalised claim. A forwarded message that quotes the rumour
/// spreads the rumour, and Ukweli should not be the thing that does that. What
/// it carries is the verdict, the date the evidence was checked, the source,
/// and a link back to the full result.
/// </para>
/// </remarks>
public static class ShareText
{
    public static string StatusWord(VerdictStatus status) =>
        StatusWord(status, Language.English);

    /// <summary>The verdict, in words, for a reader of <paramref name="language"/>.</summary>
    public static string StatusWord(VerdictStatus status, Language language) =>
        (language, status) switch
        {
            (Language.NigerianPidgin, VerdictStatus.Supported) =>
                "Official source back am",
            (Language.NigerianPidgin, VerdictStatus.Contradicted) =>
                "Official source talk say no be so",
            (Language.NigerianPidgin, VerdictStatus.MixedUnclear) =>
                "Di evidence no gree with itself",
            (Language.NigerianPidgin, VerdictStatus.InsufficientEvidence) =>
                "Evidence no dey to talk am",

            (Language.French, VerdictStatus.Supported) =>
                "Confirmé par une source officielle",
            (Language.French, VerdictStatus.Contradicted) =>
                "Démenti par une source officielle",
            (Language.French, VerdictStatus.MixedUnclear) =>
                "Éléments contradictoires",
            (Language.French, VerdictStatus.InsufficientEvidence) =>
                "Pas assez d'éléments pour se prononcer",

            (_, VerdictStatus.Supported) => "Supported by an official source",
            (_, VerdictStatus.Contradicted) => "Contradicted by an official source",
            (_, VerdictStatus.MixedUnclear) => "Mixed or unclear",
            (_, VerdictStatus.InsufficientEvidence) => "Not enough evidence to say",

            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
        };

    /// <param name="status">The verdict.</param>
    /// <param name="checkedOn">When a human last checked the cited evidence.</param>
    /// <param name="primarySourceUrl">The document behind the verdict, if any.</param>
    /// <param name="appUrl">Public base URL, used to build the /r/{id} link.</param>
    /// <param name="analysisId">The analysis id.</param>
    /// <param name="language">The language the summary is written in.</param>
    public static string Build(
        VerdictStatus status,
        DateOnly? checkedOn,
        string? primarySourceUrl,
        string appUrl,
        string analysisId,
        Language language = Language.English)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisId);

        var words = Words(language);

        var caveat = checkedOn is { } date
            ? $"{words.AsCheckedOn} {Date(date, language)}"
            : words.NothingChecked;

        var sentences = new List<string>
        {
            $"{words.Lead}: {StatusWord(status, language)}, {caveat}",
        };

        if (!string.IsNullOrWhiteSpace(primarySourceUrl) &&
            SourceValidator.IsHttpUrl(primarySourceUrl))
        {
            sentences.Add($"{words.Source}: {primarySourceUrl.Trim()}");
        }

        sentences.Add($"{words.FullResult}: {ResultUrl(appUrl, analysisId)}");

        return string.Join(". ", sentences) + ".";
    }

    /// <summary>The canonical <c>/r/{id}</c> address for an analysis.</summary>
    public static string ResultUrl(string appUrl, string analysisId) =>
        $"{appUrl.TrimEnd('/')}/r/{analysisId}";

    /// <summary>
    /// The date, spelled out. French gets its own month names; Pidgin uses the
    /// English ones, which is how dates are actually written in Nigeria.
    /// </summary>
    /// <remarks>
    /// The French months are written out here rather than taken from a culture,
    /// because the API runs in globalization-invariant mode — asking for
    /// <c>fr-FR</c> throws there, and it would have thrown in production the
    /// first time somebody read a result in French.
    /// </remarks>
    private static string Date(DateOnly date, Language language) =>
        language == Language.French
            ? $"{date.Day} {FrenchMonths[date.Month - 1]} {date.Year}"
            : date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);

    private static readonly string[] FrenchMonths =
    [
        "janvier", "février", "mars", "avril", "mai", "juin",
        "juillet", "août", "septembre", "octobre", "novembre", "décembre",
    ];

    private sealed record Phrasing(
        string Lead, string AsCheckedOn, string NothingChecked, string Source, string FullResult);

    private static Phrasing Words(Language language) => language switch
    {
        Language.NigerianPidgin => new Phrasing(
            "Ukweli check",
            "as dem check am for",
            "we no check any source",
            "Source",
            "See di full result"),

        Language.French => new Phrasing(
            "Vérification Ukweli",
            "vérifié le",
            "aucune source n'a été vérifiée",
            "Source",
            "Résultat complet"),

        _ => new Phrasing(
            "Ukweli check",
            "as checked on",
            "no source was checked",
            "Source",
            "Full result"),
    };
}
