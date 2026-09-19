using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Ukweli.Evidence;

/// <summary>
/// Turns pasted text into the single form the system stores and compares.
/// </summary>
/// <remarks>
/// Only the normalised claim is ever persisted — never the raw input. The rules
/// here are deliberately shallow: collapse whitespace, drop the decoration a
/// forwarded message accumulates, and lower-case for comparison. Nothing here
/// changes the meaning of a claim, because a claim that means something else
/// after normalisation is a different claim.
/// </remarks>
public static partial class ClaimNormaliser
{
    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace { get; }

    /// <summary>Emoji, bullets and arrows forwarded messages collect.</summary>
    /// <remarks>
    /// Includes the variation selectors (U+FE00-U+FE0F) and the zero-width
    /// joiner. Without them an emoji leaves its invisible tail behind, and two
    /// copies of the same forwarded message stop comparing equal.
    /// </remarks>
    [GeneratedRegex(
        @"[\p{So}\p{Cs}\u2022\u25AA\u25CF\u27A1\u2192\uFE00-\uFE0F\u200D\u200B]",
        RegexOptions.CultureInvariant)]
    private static partial Regex Decoration { get; }

    /// <summary>Collapses whitespace and strips decoration, preserving wording and case.</summary>
    public static string Normalise(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var stripped = Decoration.Replace(text, string.Empty);
        return Whitespace.Replace(stripped, " ").Trim();
    }

    /// <summary>
    /// The form used only for comparison: normalised, lower-cased, and with
    /// punctuation removed, so "must pay!" and "must pay" are the same claim.
    /// </summary>
    public static string ComparisonKey(string? text)
    {
        var normalised = Normalise(text);
        if (normalised.Length == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder(normalised.Length);
        var lastWasSpace = false;

        foreach (var character in normalised.ToLower(CultureInfo.InvariantCulture))
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                lastWasSpace = false;
            }
            else if (!lastWasSpace)
            {
                builder.Append(' ');
                lastWasSpace = true;
            }
        }

        return builder.ToString().Trim();
    }

    /// <summary>Whether two claim texts are the same claim for matching purposes.</summary>
    public static bool Matches(string? left, string? right) =>
        ComparisonKey(left) is { Length: > 0 } key &&
        string.Equals(key, ComparisonKey(right), StringComparison.Ordinal);
}
