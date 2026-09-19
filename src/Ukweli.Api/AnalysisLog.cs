using Ukweli.Contracts;

namespace Ukweli.Api;

/// <summary>
/// The only things Ukweli logs about an analysis: ids, verdicts and timings.
/// </summary>
/// <remarks>
/// Claim text is deliberately absent from every message here, and there is no
/// overload that accepts it. A civic claim is often someone's private
/// circumstance — a payment demand, a health worry — and logs outlive the
/// request, get shipped elsewhere and get read by people the user never met.
/// Source-generated so the analyzers are satisfied and nothing is formatted
/// when the level is disabled.
/// </remarks>
internal static partial class AnalysisLog
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Analyzed seeded claim {SeedId} as {Status}, stored as {AnalysisId} in {ElapsedMs}ms")]
    public static partial void SeededAnalysis(
        this ILogger logger, string seedId, VerdictStatus status, string analysisId, long elapsedMs);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Analyzed free-text claim as {Status}, stored as {AnalysisId} in {ElapsedMs}ms")]
    public static partial void FreeTextAnalysis(
        this ILogger logger, VerdictStatus status, string analysisId, long elapsedMs);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Model call failed after {ElapsedMs}ms: {Reason}")]
    public static partial void ModelUnavailable(this ILogger logger, long elapsedMs, string reason);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Information,
        Message = "Verdict downgraded from {From} to {To}: {Reason}")]
    public static partial void VerdictDowngraded(
        this ILogger logger, VerdictStatus from, VerdictStatus to, string reason);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "Stripped {Count} cited source id(s) the model invented")]
    public static partial void StrippedUnknownSources(this ILogger logger, int count);
}
