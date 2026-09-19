using Ukweli.Evidence;

namespace Ukweli.Evidence.Tests;

public class ClaimNormaliserTests
{
    [Fact]
    public void CollapsesTheWhitespaceAForwardedMessageCollects()
    {
        Assert.Equal(
            "Every shop must pay a new levy.",
            ClaimNormaliser.Normalise("  Every   shop\tmust\npay a new levy.  "));
    }

    [Fact]
    public void StripsForwardingDecorationWithoutChangingWords()
    {
        var result = ClaimNormaliser.Normalise("➡️ Every shop must pay a new levy. 😱");

        Assert.Equal("Every shop must pay a new levy.", result);
    }

    [Fact]
    public void PreservesWordingAndCase()
    {
        // Normalisation must not reword a claim: a claim that means something
        // else afterwards is a different claim.
        const string claim = "Lagos State Has Introduced A New Levy";

        Assert.Equal(claim, ClaimNormaliser.Normalise(claim));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public void TreatsBlankInputAsEmpty(string? input)
    {
        Assert.Equal(string.Empty, ClaimNormaliser.Normalise(input));
    }

    [Theory]
    [InlineData("Every shop must pay!", "every shop must pay")]
    [InlineData("EVERY SHOP MUST PAY", "every shop must pay")]
    [InlineData("  every  shop, must pay.  ", "every shop must pay")]
    public void BuildsAComparisonKeyIgnoringCaseAndPunctuation(string input, string expected)
    {
        Assert.Equal(expected, ClaimNormaliser.ComparisonKey(input));
    }

    [Fact]
    public void MatchesTheSameClaimWrittenDifferently()
    {
        Assert.True(ClaimNormaliser.Matches(
            "Every shop must pay a new levy!",
            "  every   shop must pay a new levy  "));
    }

    [Fact]
    public void DoesNotMatchDifferentClaims()
    {
        Assert.False(ClaimNormaliser.Matches(
            "Every shop must pay a new levy",
            "No shop must pay a new levy"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NeverMatchesOnBlankInput(string? blank)
    {
        // Otherwise every empty request would match the first seed.
        Assert.False(ClaimNormaliser.Matches(blank, blank));
        Assert.False(ClaimNormaliser.Matches("a real claim", blank));
    }
}
