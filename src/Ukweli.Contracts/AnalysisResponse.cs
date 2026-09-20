namespace Ukweli.Contracts;

/// <summary>
/// The result of checking one claim, as <c>POST /api/analyze</c> and
/// <c>GET /api/results/{id}</c> both return it.
/// </summary>
/// <param name="Id">Short id, also the <c>/r/{id}</c> share path.</param>
/// <param name="NormalizedClaim">
/// The claim as the system understood it. This, and never the raw pasted text,
/// is what gets stored.
/// </param>
/// <param name="Jurisdiction">Where the claim applies, when that could be established.</param>
/// <param name="Status">One of the four verdicts. There is no confidence score.</param>
/// <param name="Explanation">What the evidence says, in full.</param>
/// <param name="SimpleExplanation">The same evidence, shorter. Never new facts.</param>
/// <param name="Unknowns">What this check could not establish.</param>
/// <param name="Action">One safe next step.</param>
/// <param name="ActionIsGeneric">
/// True when the action is the fixed fallback caution rather than something a
/// cited excerpt actually supports. Stated plainly so the interface can show
/// the difference.
/// </param>
/// <param name="Sources">The cited sources, with how each stands to the claim.</param>
/// <param name="CheckedOn">
/// The most recent date a human checked any cited source — the date the
/// "as checked on" caveat refers to.
/// </param>
/// <param name="ShareText">
/// A short summary safe to forward. Built server-side and deliberately free of
/// the user's own words.
/// </param>
/// <param name="Language">
/// The language this rendering is in. It is not always the language that was
/// asked for: when a translation cannot be produced the English is returned and
/// <paramref name="TranslationNote"/> says so, because showing English under
/// another language's label would be a quiet lie.
/// </param>
/// <param name="AvailableLanguages">
/// Every language this result can be read in, so a client can offer them
/// without guessing.
/// </param>
/// <param name="TranslationNote">
/// Null in the normal case. Set when the requested language was unavailable.
/// </param>
public sealed record AnalysisResponse(
    string Id,
    string NormalizedClaim,
    Jurisdiction? Jurisdiction,
    VerdictStatus Status,
    string Explanation,
    string SimpleExplanation,
    IReadOnlyList<string> Unknowns,
    string Action,
    bool ActionIsGeneric,
    IReadOnlyList<AnalysisSourceResponse> Sources,
    DateOnly? CheckedOn,
    string ShareText,
    Language Language,
    IReadOnlyList<Language> AvailableLanguages,
    string? TranslationNote);

/// <summary>A cited source, with how it stands in relation to the claim.</summary>
public sealed record AnalysisSourceResponse(
    string Id,
    string Issuer,
    string Title,
    string Url,
    string Excerpt,
    DateOnly? PublishedAt,
    DateOnly? CheckedAt,
    SourceRelation Relation,
    SourceType SourceType,
    bool Placeholder);
