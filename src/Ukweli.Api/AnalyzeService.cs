using Ukweli.Contracts;
using Ukweli.Data;
using Ukweli.Data.Entities;
using Ukweli.Evidence;

namespace Ukweli.Api;

/// <summary>
/// The deterministic half of analysis: validate the input, and answer from the
/// seeded store when the claim is one Ukweli already has a hand-written result
/// for.
/// </summary>
/// <remarks>
/// A seeded claim never reaches the model. That is what lets the demo work with
/// no API key at all, and what keeps the three verdict paths from drifting.
/// </remarks>
public sealed class AnalyzeService(
    AnalysisRepository analyses,
    SeedStore seeds)
{
    /// <summary>Recorded on seeded rows in place of a model version.</summary>
    public const string SeededModelVersion = "seeded/hand-written@v1";

    /// <summary>Validates the request, returning the trimmed claim or a problem.</summary>
    public static ValidationOutcome Validate(AnalyzeRequest? request)
    {
        var text = request?.Text?.Trim() ?? string.Empty;
        var exampleId = request?.ExampleId?.Trim();

        // An example carries its own claim, so empty text is fine alongside one.
        if (text.Length == 0 && !string.IsNullOrEmpty(exampleId))
        {
            return ValidationOutcome.Valid(string.Empty, exampleId);
        }

        if (text.Length == 0)
        {
            return ValidationOutcome.Invalid(
                "Enter the claim you want checked, or choose one of the examples.");
        }

        if (text.Length < AnalyzeRequestRules.MinimumLength)
        {
            return ValidationOutcome.Invalid(
                $"That is only {text.Length} characters. Paste a bit more of the message — "
                + $"at least {AnalyzeRequestRules.MinimumLength} characters — so there is a "
                + "claim to check.");
        }

        if (text.Length > AnalyzeRequestRules.MaximumLength)
        {
            return ValidationOutcome.Invalid(
                $"That is {text.Length} characters, and the limit is "
                + $"{AnalyzeRequestRules.MaximumLength}. Paste just the part that makes the "
                + "claim.");
        }

        return ValidationOutcome.Valid(text, exampleId);
    }

    /// <summary>
    /// Finds the seeded analysis for this request, or null when the claim is
    /// not one of the seeds and must go to the model instead (Phase 3).
    /// </summary>
    public SeedRecord? MatchSeed(string text, string? exampleId) =>
        SeedCorpus.MatchById(seeds.Records, exampleId)
        ?? SeedCorpus.MatchByText(seeds.Records, text);

    /// <summary>
    /// Returns the stored analysis for a seed, creating it on first use so that
    /// it has a real id and a real share link like any other result.
    /// </summary>
    public async Task<ClaimAnalysis> ResolveSeededAsync(
        SeedRecord seed, string? userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seed);

        if (await analyses.FindBySeedAsync(seed.Id, userId, cancellationToken) is { } existing)
        {
            return existing;
        }

        var (action, actionIsGeneric) = ResolveAction(seed);

        var analysis = new ClaimAnalysis
        {
            Id = AnalysisId.New(),
            UserId = userId,
            NormalizedClaim = ClaimNormaliser.Normalise(seed.NormalizedClaim),
            Jurisdiction = seed.Jurisdiction,
            Status = seed.Status,
            Explanation = seed.Explanation,
            SimpleExplanation = seed.SimpleExplanation,
            Unknowns = [.. seed.Unknowns],
            Action = action,
            ActionIsGeneric = actionIsGeneric,
            SourceIds = [.. seed.Sources.Select(source => source.Id)],
            SourceRelations = seed.Sources.ToDictionary(
                source => source.Id, source => source.Relation),
            IsSeeded = true,
            SeedId = seed.Id,
            ModelVersion = SeededModelVersion,
            CreatedAt = DateTimeOffset.UtcNow,
        };

        return await analyses.AddAsync(analysis, cancellationToken);
    }

    /// <summary>
    /// Safety rule 7 applies to hand-written actions too. A seed that cites
    /// nothing, or whose action is not on the allowlist, gets the fixed caution.
    /// </summary>
    private static (string Action, bool IsGeneric) ResolveAction(SeedRecord seed)
    {
        // A seeded action is grounded by the fact that it cites sources at all;
        // the excerpts themselves are checked at verification time.
        var citedExcerpts = seed.Sources.Select(source => source.Id).ToList();

        return SafeAction.Resolve(seed.Action, citedExcerpts, issuer: null);
    }
}

/// <summary>The result of validating an analyze request.</summary>
public sealed record ValidationOutcome(bool IsValid, string Text, string? ExampleId, string? Message)
{
    public static ValidationOutcome Valid(string text, string? exampleId) =>
        new(true, text, exampleId, null);

    public static ValidationOutcome Invalid(string message) =>
        new(false, string.Empty, null, message);
}
