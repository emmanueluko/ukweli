using Ukweli.Contracts;
using Ukweli.Evidence;

namespace Ukweli.Evidence.Tests;

/// <summary>
/// A scripted model. Every guardrail test drives this, so the safety rules are
/// verified without a network call, an API key, or a bill.
/// </summary>
public sealed class StubAiProvider : IAiProvider
{
    public ExtractedClaim? NextExtraction { get; set; }

    public ProposedVerdict? NextVerdict { get; set; }

    /// <summary>Thrown by the next extract call, then cleared — for testing the one retry.</summary>
    public Exception? ExtractFailsOnceWith { get; set; }

    public Exception? ExtractAlwaysFailsWith { get; set; }

    public Exception? VerdictFailsWith { get; set; }

    /// <summary>What the next translate call returns. Null echoes the English back.</summary>
    public Translation? NextTranslation { get; set; }

    public Exception? TranslateFailsWith { get; set; }

    public int TranslateCalls { get; private set; }

    /// <summary>The language the last translate call asked for.</summary>
    public Language? LastTranslateLanguage { get; private set; }

    public int ExtractCalls { get; private set; }

    public int VerdictCalls { get; private set; }

    /// <summary>What the model was actually shown — used to prove rule 1.</summary>
    public IReadOnlyList<SourceRecord> LastRetrieved { get; private set; } = [];

    public Task<ExtractedClaim> ExtractAsync(string claimText, CancellationToken cancellationToken)
    {
        ExtractCalls++;

        if (ExtractAlwaysFailsWith is { } always)
        {
            throw always;
        }

        if (ExtractFailsOnceWith is { } once)
        {
            ExtractFailsOnceWith = null;
            throw once;
        }

        return Task.FromResult(
            NextExtraction ?? new ExtractedClaim(claimText, null, null, null, null));
    }

    public Task<ProposedVerdict> VerdictAsync(
        ExtractedClaim claim,
        IReadOnlyList<SourceRecord> retrieved,
        CancellationToken cancellationToken)
    {
        VerdictCalls++;
        LastRetrieved = retrieved;

        if (VerdictFailsWith is { } failure)
        {
            throw failure;
        }

        return Task.FromResult(
            NextVerdict ?? new ProposedVerdict(
                VerdictStatus.InsufficientEvidence, "No verdict scripted.", [], [], string.Empty, string.Empty));
    }

    public Task<Translation> TranslateAsync(
        Translation approved,
        Language language,
        CancellationToken cancellationToken)
    {
        TranslateCalls++;
        LastTranslateLanguage = language;

        if (TranslateFailsWith is { } failure)
        {
            throw failure;
        }

        return Task.FromResult(NextTranslation ?? approved);
    }
}
