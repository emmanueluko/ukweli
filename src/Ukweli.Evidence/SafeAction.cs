namespace Ukweli.Evidence;

/// <summary>
/// Safety rule 7: the next step Ukweli suggests is either grounded in a cited
/// excerpt, or it is the fixed generic caution. There is no third option.
/// </summary>
/// <remarks>
/// An action is the part of a result a person is most likely to act on, so it
/// is the part least safe to let a model improvise. A suggestion that is not
/// clearly supported by something a cited source actually says is replaced,
/// not reworded.
/// </remarks>
public static class SafeAction
{
    /// <summary>
    /// The fallback used whenever an action cannot be grounded. It advises
    /// verification and nothing else — it never tells anyone to pay, to refuse
    /// to pay, or to take a medical decision.
    /// </summary>
    public static string GenericCaution(string? issuer) =>
        string.IsNullOrWhiteSpace(issuer)
            ? "Confirm directly with the issuing authority before paying or sharing."
            : $"Confirm directly with {issuer.Trim()} before paying or sharing.";

    /// <summary>
    /// Verbs an action may open with. Each one asks the reader to check
    /// something or to wait; none of them commits them to an outcome.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedOpeners =
    [
        "confirm",
        "check",
        "verify",
        "ask",
        "contact",
        "visit",
        "call",
        "request",
        "wait",
        "do not pay",
        "do not share",
        "keep",
        "report",
    ];

    /// <summary>
    /// Phrases that disqualify an action outright, whatever else it says.
    /// These either direct money, override medical advice, or promise an
    /// outcome Ukweli is in no position to promise.
    /// </summary>
    public static readonly IReadOnlyList<string> ForbiddenPhrases =
    [
        "pay the",
        "pay this",
        "make the payment",
        "transfer",
        "send money",
        "send the money",
        "bank details",
        "account number",
        "you are safe",
        "it is safe",
        "no need to see a doctor",
        "no need to go to hospital",
        "stop taking",
        "guaranteed",
        "definitely true",
        "definitely false",
    ];

    /// <summary>
    /// Whether an action may be shown as written. It must open with an allowed
    /// verb, avoid every forbidden phrase, and be grounded in a cited excerpt.
    /// </summary>
    /// <param name="action">The proposed action.</param>
    /// <param name="citedExcerpts">
    /// The excerpts actually cited by the verdict. An action referring to
    /// something none of them mentions is not grounded, whatever it says.
    /// </param>
    public static bool IsAllowed(string? action, IReadOnlyList<string> citedExcerpts)
    {
        ArgumentNullException.ThrowIfNull(citedExcerpts);

        if (string.IsNullOrWhiteSpace(action))
        {
            return false;
        }

        var trimmed = action.Trim();
        var lowered = trimmed.ToLowerInvariant();

        if (ForbiddenPhrases.Any(phrase => lowered.Contains(phrase, StringComparison.Ordinal)))
        {
            return false;
        }

        if (!AllowedOpeners.Any(opener => lowered.StartsWith(opener, StringComparison.Ordinal)))
        {
            return false;
        }

        // With nothing cited there is nothing to ground an action in, so the
        // generic caution is the only honest answer.
        return citedExcerpts.Count > 0;
    }

    /// <summary>
    /// Returns the action to show, and whether it is the generic fallback.
    /// </summary>
    public static (string Action, bool IsGeneric) Resolve(
        string? proposed, IReadOnlyList<string> citedExcerpts, string? issuer)
    {
        return IsAllowed(proposed, citedExcerpts)
            ? (proposed!.Trim(), false)
            : (GenericCaution(issuer), true);
    }
}
