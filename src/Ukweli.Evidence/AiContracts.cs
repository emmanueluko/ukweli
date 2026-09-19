using Ukweli.Contracts;

namespace Ukweli.Evidence;

/// <summary>What the extract step returns: the claim, restated, and nothing judged.</summary>
/// <param name="NormalizedClaim">One sentence stating what the message asserts.</param>
/// <param name="Jurisdiction">
/// Where the claim applies, or null. Null is expected and useful: a claim that
/// names no place has no jurisdiction, and guessing one sends the check to the
/// wrong evidence.
/// </param>
/// <param name="EffectiveDate">The date the claim says something starts, or null.</param>
/// <param name="AffectedGroup">Who the message says is affected, or null.</param>
/// <param name="RequestedAction">What the message tells the reader to do, or null.</param>
public sealed record ExtractedClaim(
    string NormalizedClaim,
    Jurisdiction? Jurisdiction,
    DateOnly? EffectiveDate,
    string? AffectedGroup,
    string? RequestedAction);

/// <summary>
/// What the verdict step returns. Every field is a proposal, not a decision:
/// the server validates all of it before any of it reaches a user.
/// </summary>
public sealed record ProposedVerdict(
    VerdictStatus Status,
    string Rationale,
    IReadOnlyList<string> CitedSourceIds,
    IReadOnlyList<string> Unknowns,
    string Action,
    string SimpleExplanation);

/// <summary>
/// The model, behind an interface.
/// </summary>
/// <remarks>
/// Every guardrail test drives a stub through this, so the safety rules are
/// verified without a network call, an API key, or a bill. The real
/// implementation lives in the API project; this interface and everything that
/// validates its output live here, where no HTTP dependency is allowed.
/// </remarks>
public interface IAiProvider
{
    /// <summary>Normalises a pasted message into a checkable claim.</summary>
    Task<ExtractedClaim> ExtractAsync(string claimText, CancellationToken cancellationToken);

    /// <summary>
    /// Drafts a verdict from the claim and the retrieved excerpts.
    /// </summary>
    /// <param name="retrieved">
    /// The complete set of excerpts the model may consider. It receives nothing
    /// else — no web access, and no way to add a source (safety rule 1).
    /// </param>
    Task<ProposedVerdict> VerdictAsync(
        ExtractedClaim claim,
        IReadOnlyList<SourceRecord> retrieved,
        CancellationToken cancellationToken);
}

/// <summary>Raised when the model fails, times out, or returns something unusable.</summary>
/// <remarks>
/// Always surfaces as a retryable 503. Safety rule 8: a failed model call never
/// produces a fabricated result.
/// </remarks>
public sealed class AiUnavailableException : Exception
{
    public AiUnavailableException(string message) : base(message) { }

    public AiUnavailableException(string message, Exception innerException)
        : base(message, innerException) { }

    public AiUnavailableException() : base("The analysis model is unavailable.") { }
}
