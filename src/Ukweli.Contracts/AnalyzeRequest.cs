namespace Ukweli.Contracts;

/// <summary>The body of <c>POST /api/analyze</c>.</summary>
/// <param name="Text">
/// The claim to check, as the user pasted it. Only the normalised form is ever
/// stored; the raw text is never persisted and never logged.
/// </param>
/// <param name="ExampleId">
/// Set when the user picked a ready-made example instead of typing. When it
/// matches a seeded claim the stored analysis is returned directly.
/// </param>
public sealed record AnalyzeRequest(string? Text, string? ExampleId);

public static class AnalyzeRequestRules
{
    /// <summary>Below this a "claim" is a fragment nothing could be checked against.</summary>
    public const int MinimumLength = 20;

    /// <summary>Above this the input is a document, not a claim.</summary>
    public const int MaximumLength = 1000;
}
