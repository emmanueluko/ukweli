using System.Globalization;
using Ukweli.Contracts;

namespace Ukweli.Evidence;

/// <summary>
/// Builds the short summary a user forwards.
/// </summary>
/// <remarks>
/// <para>
/// This is the part of a result most likely to travel further than the page it
/// came from, so it is built here, server-side, and never assembled by a client
/// or a model.
/// </para>
/// <para>
/// It deliberately carries none of the user's own words — not the pasted text
/// and not the normalised claim. A forwarded message that quotes the rumour
/// spreads the rumour, and Ukweli should not be the thing that does that. What
/// it carries is the verdict, the date the evidence was checked, the source,
/// and a link back to the full result.
/// </para>
/// </remarks>
public static class ShareText
{
    public static string StatusWord(VerdictStatus status) => status switch
    {
        VerdictStatus.Supported => "Supported by an official source",
        VerdictStatus.Contradicted => "Contradicted by an official source",
        VerdictStatus.MixedUnclear => "Mixed or unclear",
        VerdictStatus.InsufficientEvidence => "Not enough evidence to say",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, null),
    };

    /// <param name="status">The verdict.</param>
    /// <param name="checkedOn">When a human last checked the cited evidence.</param>
    /// <param name="primarySourceUrl">The document behind the verdict, if any.</param>
    /// <param name="appUrl">Public base URL, used to build the /r/{id} link.</param>
    /// <param name="analysisId">The analysis id.</param>
    public static string Build(
        VerdictStatus status,
        DateOnly? checkedOn,
        string? primarySourceUrl,
        string appUrl,
        string analysisId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(analysisId);

        var caveat = checkedOn is { } date
            ? $"as checked on {date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)}"
            : "no source was checked";

        var sentences = new List<string> { $"Ukweli check: {StatusWord(status)}, {caveat}" };

        if (!string.IsNullOrWhiteSpace(primarySourceUrl) &&
            SourceValidator.IsHttpUrl(primarySourceUrl))
        {
            sentences.Add($"Source: {primarySourceUrl.Trim()}");
        }

        sentences.Add($"Full result: {ResultUrl(appUrl, analysisId)}");

        return string.Join(". ", sentences) + ".";
    }

    /// <summary>The canonical <c>/r/{id}</c> address for an analysis.</summary>
    public static string ResultUrl(string appUrl, string analysisId) =>
        $"{appUrl.TrimEnd('/')}/r/{analysisId}";
}
