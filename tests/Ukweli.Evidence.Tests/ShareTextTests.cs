using Ukweli.Contracts;
using Ukweli.Evidence;

namespace Ukweli.Evidence.Tests;

/// <summary>
/// Share text is the part of a result most likely to travel further than the
/// page it came from. It must carry the verdict, the caveat, the source and the
/// link — and none of the user's words.
/// </summary>
public class ShareTextTests
{
    private const string AppUrl = "https://ukweli.example";
    private const string AnalysisId = "aB3dE5gH7j";
    private const string SourceUrl = "https://ncdc.gov.ng/sitreps/lassa-w33-2026.pdf";

    private static readonly DateOnly Checked = new(2026, 9, 1);

    [Fact]
    public void CarriesTheVerdictTheCaveatTheSourceAndTheLink()
    {
        var text = ShareText.Build(
            VerdictStatus.Supported, Checked, SourceUrl, AppUrl, AnalysisId);

        Assert.Contains("Supported", text, StringComparison.Ordinal);
        Assert.Contains("as checked on 1 September 2026", text, StringComparison.Ordinal);
        Assert.Contains(SourceUrl, text, StringComparison.Ordinal);
        Assert.Contains($"{AppUrl}/r/{AnalysisId}", text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(VerdictStatus.Supported)]
    [InlineData(VerdictStatus.Contradicted)]
    [InlineData(VerdictStatus.MixedUnclear)]
    [InlineData(VerdictStatus.InsufficientEvidence)]
    public void NeverContainsTheUsersOwnWords(VerdictStatus status)
    {
        // The rumour is what the user pasted. A share message that repeats it
        // spreads it, which is the opposite of the point.
        const string pastedClaim = "every small shop must pay a new levy or be sealed";

        var text = ShareText.Build(status, Checked, SourceUrl, AppUrl, AnalysisId);

        Assert.DoesNotContain(pastedClaim, text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("shop", text, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(VerdictStatus.Supported)]
    [InlineData(VerdictStatus.Contradicted)]
    [InlineData(VerdictStatus.MixedUnclear)]
    [InlineData(VerdictStatus.InsufficientEvidence)]
    public void NeverImpliesAConfidenceScore(VerdictStatus status)
    {
        var text = ShareText.Build(status, Checked, SourceUrl, AppUrl, AnalysisId);

        Assert.DoesNotContain("%", text, StringComparison.Ordinal);
        Assert.DoesNotContain("confidence", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("likely", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("probably", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SaysNoSourceWasCheckedWhenThereIsNoDate()
    {
        // Insufficient evidence means nothing was checked, so claiming a date
        // would be claiming work that never happened.
        var text = ShareText.Build(
            VerdictStatus.InsufficientEvidence, null, null, AppUrl, AnalysisId);

        Assert.Contains("no source was checked", text, StringComparison.Ordinal);
        Assert.DoesNotContain("as checked on", text, StringComparison.Ordinal);
    }

    [Fact]
    public void OmitsTheSourceLineWhenNothingWasCited()
    {
        var text = ShareText.Build(
            VerdictStatus.InsufficientEvidence, null, null, AppUrl, AnalysisId);

        Assert.DoesNotContain("Source:", text, StringComparison.Ordinal);
        Assert.Contains($"{AppUrl}/r/{AnalysisId}", text, StringComparison.Ordinal);
    }

    [Fact]
    public void RefusesAPlaceholderUrlAsASource()
    {
        // An uncurated record's url is not an address, and must never be
        // offered as the source behind a verdict.
        var text = ShareText.Build(
            VerdictStatus.Supported,
            Checked,
            "PLACEHOLDER — curator must replace with the specific URL",
            AppUrl,
            AnalysisId);

        Assert.DoesNotContain("PLACEHOLDER", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Source:", text, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildsTheResultUrlWithoutDoublingSlashes()
    {
        Assert.Equal(
            "https://ukweli.example/r/aB3dE5gH7j",
            ShareText.ResultUrl("https://ukweli.example/", AnalysisId));
    }

    [Theory]
    [InlineData(VerdictStatus.Supported, "Supported by an official source")]
    [InlineData(VerdictStatus.Contradicted, "Contradicted by an official source")]
    [InlineData(VerdictStatus.MixedUnclear, "Mixed or unclear")]
    [InlineData(VerdictStatus.InsufficientEvidence, "Not enough evidence to say")]
    public void UsesPlainWordsForEachVerdict(VerdictStatus status, string expected)
    {
        Assert.Equal(expected, ShareText.StatusWord(status));
    }
}
