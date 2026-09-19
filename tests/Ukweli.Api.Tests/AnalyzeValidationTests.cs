using Ukweli.Contracts;

namespace Ukweli.Api.Tests;

/// <summary>
/// The input edges the specification names: 19, 20, 1000 and 1001 characters,
/// blank, and whitespace only.
/// </summary>
public class AnalyzeValidationTests
{
    private static string Text(int length) => new('a', length);

    [Fact]
    public void RejectsOneCharacterBelowTheMinimum()
    {
        var outcome = AnalyzeService.Validate(new AnalyzeRequest(Text(19), null));

        Assert.False(outcome.IsValid);
        Assert.Contains("19 characters", outcome.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public void AcceptsExactlyTheMinimum()
    {
        Assert.True(AnalyzeService.Validate(new AnalyzeRequest(Text(20), null)).IsValid);
    }

    [Fact]
    public void AcceptsExactlyTheMaximum()
    {
        Assert.True(AnalyzeService.Validate(new AnalyzeRequest(Text(1000), null)).IsValid);
    }

    [Fact]
    public void RejectsOneCharacterAboveTheMaximum()
    {
        var outcome = AnalyzeService.Validate(new AnalyzeRequest(Text(1001), null));

        Assert.False(outcome.IsValid);
        Assert.Contains("1001 characters", outcome.Message!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void RejectsBlankInput(string? text)
    {
        var outcome = AnalyzeService.Validate(new AnalyzeRequest(text, null));

        Assert.False(outcome.IsValid);
        Assert.Contains("Enter the claim", outcome.Message!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("     ")]
    [InlineData("\t\t")]
    [InlineData("\n \n")]
    public void RejectsWhitespaceOnlyInput(string text)
    {
        Assert.False(AnalyzeService.Validate(new AnalyzeRequest(text, null)).IsValid);
    }

    [Fact]
    public void MeasuresLengthAfterTrimming()
    {
        // Padding a 19-character claim with spaces must not sneak it past.
        var padded = $"          {Text(19)}          ";

        Assert.False(AnalyzeService.Validate(new AnalyzeRequest(padded, null)).IsValid);
    }

    [Fact]
    public void AcceptsAnExampleIdWithNoText()
    {
        // Picking an example supplies the claim, so there is nothing to type.
        var outcome = AnalyzeService.Validate(new AnalyzeRequest(null, "seed-lagos-shop-levy"));

        Assert.True(outcome.IsValid);
        Assert.Equal("seed-lagos-shop-levy", outcome.ExampleId);
    }

    [Fact]
    public void ExplainsTheLimitRatherThanJustRefusing()
    {
        var outcome = AnalyzeService.Validate(new AnalyzeRequest(Text(5), null));

        Assert.False(outcome.IsValid);
        Assert.Contains("at least 20", outcome.Message!, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsANullRequestBody()
    {
        Assert.False(AnalyzeService.Validate(null).IsValid);
    }
}
