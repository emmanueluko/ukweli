namespace Ukweli.Evidence;

/// <summary>
/// Safety rule 8's retry policy: one retry, on extraction only.
/// </summary>
/// <remarks>
/// <para>
/// Extraction is retried because it is cheap, deterministic in intent, and a
/// transient failure there costs the whole check.
/// </para>
/// <para>
/// The verdict call is never retried. A second attempt over the same excerpts
/// is as likely to differ as to agree, and a verdict that changes between
/// attempts is not a verdict — it is a coin toss wearing one's clothes. A
/// failed verdict call surfaces as a 503 instead.
/// </para>
/// <para>
/// This lives beside the other safety rules, rather than in the pipeline that
/// calls it, so the policy is testable without a server.
/// </para>
/// </remarks>
public static class AiRetry
{
    public const int ExtractAttempts = 2;

    /// <summary>Runs extraction, retrying once if the model is unavailable.</summary>
    public static async Task<ExtractedClaim> ExtractAsync(
        IAiProvider provider, string claimText, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);

        try
        {
            return await provider.ExtractAsync(claimText, cancellationToken);
        }
        catch (AiUnavailableException) when (!cancellationToken.IsCancellationRequested)
        {
            // Exactly one more attempt. If it fails too, the caller answers 503.
            return await provider.ExtractAsync(claimText, cancellationToken);
        }
    }
}
