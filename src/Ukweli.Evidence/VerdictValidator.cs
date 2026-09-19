using Ukweli.Contracts;

namespace Ukweli.Evidence;

/// <summary>
/// Turns a model's proposal into the verdict Ukweli will actually stand behind.
/// </summary>
/// <remarks>
/// <para>
/// This is where safety rules 2 to 7 are enforced, and it is the reason the
/// model drafts text while the server owns every decision. Nothing the model
/// returns is trusted: cited ids are checked against what was actually
/// retrieved, a verdict that loses its support is downgraded rather than
/// shown, and an ungrounded action is replaced rather than reworded.
/// </para>
/// <para>
/// Pure, and deliberately so. Every rule here is testable against a stub with
/// no network, no database and no clock.
/// </para>
/// </remarks>
public static class VerdictValidator
{
    /// <summary>The verdict as validated, with a record of what was changed and why.</summary>
    /// <param name="Status">The verdict actually returned.</param>
    /// <param name="ProposedStatus">What the model asked for.</param>
    /// <param name="Explanation">The rationale, as drafted.</param>
    /// <param name="SimpleExplanation">The short form.</param>
    /// <param name="CitedSourceIds">Cited ids that survived validation.</param>
    /// <param name="Relations">How each surviving source stands to the claim.</param>
    /// <param name="Unknowns">What remains unknown.</param>
    /// <param name="Action">The next step actually shown.</param>
    /// <param name="ActionIsGeneric">Whether that step is the fixed caution.</param>
    /// <param name="StrippedSourceIds">Ids the model cited that do not exist.</param>
    /// <param name="DowngradeReason">Why the verdict changed, or null if it did not.</param>
    public sealed record Result(
        VerdictStatus Status,
        VerdictStatus ProposedStatus,
        string Explanation,
        string SimpleExplanation,
        IReadOnlyList<string> CitedSourceIds,
        IReadOnlyDictionary<string, SourceRelation> Relations,
        IReadOnlyList<string> Unknowns,
        string Action,
        bool ActionIsGeneric,
        IReadOnlyList<string> StrippedSourceIds,
        string? DowngradeReason)
    {
        public bool WasDowngraded => Status != ProposedStatus;
    }

    /// <summary>
    /// Safety rule 4. Empty retrieval is <c>insufficient_evidence</c>, and the
    /// verdict call is skipped entirely — there is nothing to ask about.
    /// Absence of evidence is never treated as disproof.
    /// </summary>
    public static Result ForEmptyRetrieval(string claimSubject)
    {
        var explanation =
            "The curated store holds no source covering this, so Ukweli cannot say whether it "
            + "is true or false. That is not evidence the claim is false — it means this check "
            + "found nothing either way.";

        return new Result(
            Status: VerdictStatus.InsufficientEvidence,
            ProposedStatus: VerdictStatus.InsufficientEvidence,
            Explanation: explanation,
            SimpleExplanation: "Ukweli has no source covering this, so it cannot check it.",
            CitedSourceIds: [],
            Relations: new Dictionary<string, SourceRelation>(),
            Unknowns:
            [
                "Whether any official notice on this subject exists at all.",
                string.IsNullOrWhiteSpace(claimSubject)
                    ? "Which authority the claim refers to."
                    : $"Which authority is said to be responsible for {claimSubject}.",
            ],
            Action: SafeAction.GenericCaution(null),
            ActionIsGeneric: true,
            StrippedSourceIds: [],
            DowngradeReason: null);
    }

    /// <summary>
    /// Applies safety rules 2, 3, 5, 6 and 7 to a model's proposal.
    /// </summary>
    /// <param name="proposal">What the model returned. Entirely untrusted.</param>
    /// <param name="retrieved">
    /// The excerpts actually given to the model. Any id outside this set was
    /// invented.
    /// </param>
    public static Result Validate(ProposedVerdict proposal, IReadOnlyList<SourceRecord> retrieved)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentNullException.ThrowIfNull(retrieved);

        // Safety rule 2: an id the model invented is stripped, not looked up.
        var byId = retrieved.ToDictionary(source => source.Id, StringComparer.Ordinal);

        var cited = proposal.CitedSourceIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var valid = cited.Where(byId.ContainsKey).ToList();
        var stripped = cited.Where(id => !byId.ContainsKey(id)).ToList();

        var citedSources = valid.Select(id => byId[id]).ToList();
        var relations = AssignRelations(proposal.Status, citedSources);

        var (status, reason) = Downgrade(proposal.Status, citedSources, relations, stripped);

        // Safety rule 7: the action is grounded in a cited excerpt, or replaced.
        var citedExcerpts = citedSources.Select(source => source.Excerpt).ToList();
        var issuer = citedSources.FirstOrDefault()?.Issuer;
        var (action, actionIsGeneric) = SafeAction.Resolve(proposal.Action, citedExcerpts, issuer);

        return new Result(
            Status: status,
            ProposedStatus: proposal.Status,
            Explanation: Scrub(proposal.Rationale),
            SimpleExplanation: Scrub(proposal.SimpleExplanation),
            CitedSourceIds: valid,
            Relations: relations,
            Unknowns: [.. proposal.Unknowns.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim())],
            Action: action,
            ActionIsGeneric: actionIsGeneric,
            StrippedSourceIds: stripped,
            DowngradeReason: reason);
    }

    /// <summary>
    /// Safety rules 3 and 5. A verdict must rest on a primary official source;
    /// a secondary source never overrides a primary, and on conflict the answer
    /// is <c>mixed_unclear</c>.
    /// </summary>
    private static (VerdictStatus Status, string? Reason) Downgrade(
        VerdictStatus proposed,
        List<SourceRecord> cited,
        IReadOnlyDictionary<string, SourceRelation> relations,
        List<string> stripped)
    {
        if (cited.Count == 0)
        {
            return proposed == VerdictStatus.InsufficientEvidence
                ? (proposed, null)
                : (VerdictStatus.InsufficientEvidence,
                    stripped.Count > 0
                        ? "Every cited source id was invented, leaving nothing behind the verdict."
                        : "The verdict cited no source.");
        }

        var primaries = cited.Where(source => source.IsPrimary).ToList();

        // Safety rule 5: a claim both supported and contradicted by primary
        // sources is unclear, whatever the model concluded.
        var primarySupports = primaries.Any(
            source => relations.GetValueOrDefault(source.Id) == SourceRelation.Supports);
        var primaryConflicts = primaries.Any(
            source => relations.GetValueOrDefault(source.Id) == SourceRelation.Conflicts);

        if (primarySupports && primaryConflicts)
        {
            return (VerdictStatus.MixedUnclear,
                "Primary sources disagree about this claim.");
        }

        return proposed switch
        {
            VerdictStatus.Supported when !primarySupports => (
                VerdictStatus.MixedUnclear,
                primaries.Count == 0
                    ? "Only secondary sources spoke to this claim; a secondary source cannot "
                        + "establish it on its own."
                    : "No primary source supports the claim as stated."),

            VerdictStatus.Contradicted when !primaryConflicts => (
                VerdictStatus.MixedUnclear,
                primaries.Count == 0
                    ? "Only secondary sources spoke to this claim; a secondary source cannot "
                        + "contradict it on its own."
                    : "No primary source contradicts the claim as stated."),

            _ => (proposed, null),
        };
    }

    /// <summary>
    /// Derives how each cited source stands to the claim from the verdict the
    /// model proposed. A primary source carries the verdict's relation; a
    /// secondary source is context, because rule 5 says it can never be what a
    /// verdict rests on.
    /// </summary>
    private static Dictionary<string, SourceRelation> AssignRelations(
        VerdictStatus proposed, List<SourceRecord> cited)
    {
        var relation = proposed switch
        {
            VerdictStatus.Supported => SourceRelation.Supports,
            VerdictStatus.Contradicted => SourceRelation.Conflicts,
            _ => SourceRelation.Context,
        };

        return cited.ToDictionary(
            source => source.Id,
            source => source.IsPrimary ? relation : SourceRelation.Context,
            StringComparer.Ordinal);
    }

    /// <summary>
    /// Safety rule 6. Removes any numeric confidence the model slipped into its
    /// prose, so no copy anywhere implies a probability.
    /// </summary>
    public static string Scrub(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var cleaned = text.Trim();

        foreach (var phrase in ConfidencePhrases)
        {
            cleaned = cleaned.Replace(phrase, string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Hedges that read as a probability without stating one.</summary>
    private static readonly string[] ConfidencePhrases =
    [
        "with high confidence",
        "with low confidence",
        "with moderate confidence",
        "high confidence",
        "low confidence",
        "confidence level",
    ];

    /// <summary>Whether any prose in a result implies a confidence score.</summary>
    public static bool ImpliesConfidence(string? text) =>
        !string.IsNullOrWhiteSpace(text) &&
        (text.Contains('%', StringComparison.Ordinal) ||
         text.Contains("confidence", StringComparison.OrdinalIgnoreCase));
}
