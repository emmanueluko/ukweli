using Ukweli.Contracts;
using Ukweli.Data;
using Ukweli.Data.Entities;
using Ukweli.Evidence;

namespace Ukweli.Api.Ai;

/// <summary>
/// The free-text path: extract, retrieve, draft a verdict, then validate it.
/// </summary>
/// <remarks>
/// The model drafts; this class decides. Every step after the model's output
/// runs through the pure functions in <c>Ukweli.Evidence</c>, and the result is
/// persisted only after those have had their say.
/// </remarks>
public sealed class AnalysisPipeline(
    IAiProvider ai,
    SourceRepository sources,
    AnalysisRepository analyses,
    ILogger<AnalysisPipeline> logger)
{
    public async Task<ClaimAnalysis> RunAsync(
        string claimText, string? userId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimText);

        // 1. Extract. AiRetry allows exactly one retry here and none on the
        //    verdict call — safety rule 8.
        var extracted = await AiRetry.ExtractAsync(ai, claimText, cancellationToken);

        // A location-dependent claim that names no place cannot be checked: the
        // honest answer is to ask which place, not to guess one.
        if (extracted.Jurisdiction is null && MentionsSomewhere(extracted))
        {
            return await PersistAsync(
                ScopedLimitation(extracted), extracted, userId,
                PromptLibrary.ExtractVersion, cancellationToken);
        }

        // 2. Retrieve. No AI: keyword scoring over the curated store.
        var corpus = await LoadCorpusAsync(cancellationToken);
        var retrieved = Retriever
            .Retrieve(corpus, extracted.NormalizedClaim, extracted.Jurisdiction)
            .Select(match => match.Source)
            .ToList();

        // 3. Safety rule 4: nothing retrieved means the verdict call is skipped
        //    entirely. Absence of evidence is never treated as disproof.
        if (retrieved.Count == 0)
        {
            return await PersistAsync(
                VerdictValidator.ForEmptyRetrieval(extracted.AffectedGroup ?? string.Empty),
                extracted, userId, PromptLibrary.ExtractVersion, cancellationToken);
        }

        // 4. Verdict. The model sees the claim and these excerpts, nothing else.
        var proposal = await ai.VerdictAsync(extracted, retrieved, cancellationToken);

        // 5. Validate. Safety rules 2, 3, 5, 6 and 7.
        var validated = VerdictValidator.Validate(proposal, retrieved);

        if (validated.StrippedSourceIds.Count > 0)
        {
            logger.StrippedUnknownSources(validated.StrippedSourceIds.Count);
        }

        if (validated.WasDowngraded)
        {
            logger.VerdictDowngraded(
                validated.ProposedStatus, validated.Status, validated.DowngradeReason ?? "unstated");
        }

        return await PersistAsync(
            validated, extracted, userId, PromptLibrary.VerdictVersion, cancellationToken);
    }

    /// <summary>
    /// Whether the claim is about somewhere in particular without saying where.
    /// </summary>
    private static bool MentionsSomewhere(ExtractedClaim claim)
    {
        var terms = Retriever.Tokenise(
            $"{claim.NormalizedClaim} {claim.AffectedGroup} {claim.RequestedAction}");

        return LocationWords.Any(terms.Contains);
    }

    private static readonly string[] LocationWords =
    [
        "area", "state", "local", "government", "council", "community", "district",
        "ward", "town", "village", "street", "market", "neighbourhood", "neighborhood",
    ];

    /// <summary>
    /// The answer for a location-dependent claim that names no location: say so,
    /// and ask for the missing piece rather than checking the wrong place.
    /// </summary>
    private static VerdictValidator.Result ScopedLimitation(ExtractedClaim claim)
    {
        var explanation =
            "This claim depends on where it applies, and the message does not say. Ukweli's "
            + "sources are specific to a state or to Nigeria as a whole, so checking it "
            + "without knowing the place would mean checking the wrong evidence. Send the "
            + "claim again naming the state or local government area it refers to.";

        return new VerdictValidator.Result(
            Status: VerdictStatus.InsufficientEvidence,
            ProposedStatus: VerdictStatus.InsufficientEvidence,
            Explanation: explanation,
            SimpleExplanation:
                "Ukweli needs to know which state or area this is about before it can check it.",
            CitedSourceIds: [],
            Relations: new Dictionary<string, SourceRelation>(),
            Unknowns:
            [
                "Which state or local government area the claim refers to.",
                claim.RequestedAction is { } action
                    ? $"Which authority is said to require: {action}"
                    : "Which authority is said to be responsible.",
            ],
            Action: SafeAction.GenericCaution(null),
            ActionIsGeneric: true,
            StrippedSourceIds: [],
            DowngradeReason: null);
    }

    private async Task<IReadOnlyList<SourceRecord>> LoadCorpusAsync(
        CancellationToken cancellationToken)
    {
        var rows = await sources.ListActiveAsync(cancellationToken);

        return [.. rows.Select(row => new SourceRecord
        {
            Id = row.Id,
            Issuer = row.Issuer,
            Title = row.Title,
            SourceType = row.SourceType,
            Jurisdiction = row.Jurisdiction,
            Topic = row.Topic,
            PublishedAt = row.PublishedAt,
            CheckedAt = row.CheckedAt,
            Url = row.Url,
            CollectionUrl = row.CollectionUrl,
            Excerpt = row.Excerpt,
            Placeholder = row.Placeholder,
            Active = row.Active,
        })];
    }

    private async Task<ClaimAnalysis> PersistAsync(
        VerdictValidator.Result validated,
        ExtractedClaim extracted,
        string? userId,
        string promptVersion,
        CancellationToken cancellationToken)
    {
        var analysis = new ClaimAnalysis
        {
            Id = AnalysisId.New(),
            UserId = userId,
            // Only the normalised claim is stored; the raw text never is.
            NormalizedClaim = ClaimNormaliser.Normalise(extracted.NormalizedClaim),
            Jurisdiction = extracted.Jurisdiction,
            Status = validated.Status,
            Explanation = validated.Explanation,
            SimpleExplanation = validated.SimpleExplanation,
            Unknowns = [.. validated.Unknowns],
            Action = validated.Action,
            ActionIsGeneric = validated.ActionIsGeneric,
            SourceIds = [.. validated.CitedSourceIds],
            SourceRelations = new Dictionary<string, SourceRelation>(validated.Relations),
            IsSeeded = false,
            SeedId = null,
            ModelVersion = AnthropicAiProvider.VersionFor(promptVersion),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return await analyses.AddAsync(analysis, cancellationToken);
    }
}
