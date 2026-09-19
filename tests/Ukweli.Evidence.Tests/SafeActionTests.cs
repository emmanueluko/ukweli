using Ukweli.Evidence;

namespace Ukweli.Evidence.Tests;

/// <summary>
/// Safety rule 7. The action is the part of a result a person is most likely to
/// act on, so it is the part least safe to let a model improvise.
/// </summary>
public class SafeActionTests
{
    private static readonly string[] SomeExcerpt = ["ncdc-lassa-sitrep-w33-2026"];
    private static readonly string[] NothingCited = [];

    [Theory]
    [InlineData("Confirm the notice with the Lagos State Internal Revenue Service before paying.")]
    [InlineData("Check the published schedule on the issuing body's own website.")]
    [InlineData("Verify the demand with the agency named on it.")]
    [InlineData("Do not pay until you have confirmed the demand with the issuer.")]
    [InlineData("Wait for an official announcement before acting on this message.")]
    [InlineData("Report the message to the agency it claims to come from.")]
    public void AllowsAnActionThatAsksTheReaderToCheckSomething(string action)
    {
        Assert.True(SafeAction.IsAllowed(action, SomeExcerpt));
    }

    [Theory]
    [InlineData("Pay the levy at the nearest office before the deadline.")]
    [InlineData("Transfer the amount to the account on the notice.")]
    [InlineData("Send money to the collector when he calls.")]
    [InlineData("You are safe, no need to go to hospital.")]
    [InlineData("Stop taking the medication your doctor prescribed.")]
    [InlineData("This is definitely true, act on it now.")]
    public void RefusesAnActionThatDirectsMoneyOrOverridesCare(string action)
    {
        Assert.False(SafeAction.IsAllowed(action, SomeExcerpt));
    }

    [Fact]
    public void RefusesAnyActionWhenNothingWasCited()
    {
        // With no evidence cited there is nothing to ground an action in, so
        // the generic caution is the only honest answer.
        Assert.False(SafeAction.IsAllowed(
            "Confirm the notice with the issuing authority.", NothingCited));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RefusesABlankAction(string? action)
    {
        Assert.False(SafeAction.IsAllowed(action, SomeExcerpt));
    }

    [Fact]
    public void RefusesAnActionThatOpensWithAVerbNotOnTheAllowlist()
    {
        Assert.False(SafeAction.IsAllowed(
            "Ignore the message entirely, it is a scam.", SomeExcerpt));
    }

    [Fact]
    public void FallsBackToTheGenericCautionNamingTheIssuer()
    {
        var (action, isGeneric) = SafeAction.Resolve(
            "Pay the levy immediately.", SomeExcerpt, "Lagos State Government");

        Assert.True(isGeneric);
        Assert.Equal(
            "Confirm directly with Lagos State Government before paying or sharing.", action);
    }

    [Fact]
    public void FallsBackWithoutAnIssuerWhenNoneIsKnown()
    {
        var (action, isGeneric) = SafeAction.Resolve(null, NothingCited, null);

        Assert.True(isGeneric);
        Assert.Equal(
            "Confirm directly with the issuing authority before paying or sharing.", action);
    }

    [Fact]
    public void KeepsAGroundedActionAsWritten()
    {
        const string grounded = "Confirm the notice with the agency named on it before paying.";

        var (action, isGeneric) = SafeAction.Resolve(grounded, SomeExcerpt, "Someone");

        Assert.False(isGeneric);
        Assert.Equal(grounded, action);
    }

    [Fact]
    public void TheGenericCautionNeverTellsAnyoneToPayOrNotToPay()
    {
        foreach (var issuer in new string?[] { null, "Lagos State Government" })
        {
            var caution = SafeAction.GenericCaution(issuer);

            Assert.StartsWith("Confirm", caution, StringComparison.Ordinal);
            Assert.Contains("before paying or sharing", caution, StringComparison.Ordinal);
        }
    }
}
