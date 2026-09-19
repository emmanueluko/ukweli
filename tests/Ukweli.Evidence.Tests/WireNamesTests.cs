using Ukweli.Contracts;

namespace Ukweli.Evidence.Tests;

/// <summary>
/// The database and the API must spell an enum the same way. These assertions
/// pin the exact strings the specification uses.
/// </summary>
public class WireNamesTests
{
    [Theory]
    [InlineData(VerdictStatus.Supported, "supported")]
    [InlineData(VerdictStatus.Contradicted, "contradicted")]
    [InlineData(VerdictStatus.MixedUnclear, "mixed_unclear")]
    [InlineData(VerdictStatus.InsufficientEvidence, "insufficient_evidence")]
    public void SpellsEveryVerdictAsTheSpecificationDoes(VerdictStatus status, string expected)
    {
        Assert.Equal(expected, WireNames.ToWire(status));
    }

    [Theory]
    [InlineData(SourceType.PrimaryOfficial, "primary_official")]
    [InlineData(SourceType.SecondaryTrusted, "secondary_trusted")]
    public void SpellsEverySourceType(SourceType sourceType, string expected)
    {
        Assert.Equal(expected, WireNames.ToWire(sourceType));
    }

    [Theory]
    [InlineData(Jurisdiction.Ng, "NG")]
    [InlineData(Jurisdiction.NgLa, "NG-LA")]
    public void SpellsEveryJurisdiction(Jurisdiction jurisdiction, string expected)
    {
        Assert.Equal(expected, WireNames.ToWire(jurisdiction));
    }

    [Theory]
    [InlineData(Topic.PaymentsLevies, "payments_levies")]
    [InlineData(Topic.DiseaseOutbreaks, "disease_outbreaks")]
    public void SpellsEveryTopic(Topic topic, string expected)
    {
        Assert.Equal(expected, WireNames.ToWire(topic));
    }

    [Theory]
    [InlineData(SourceRelation.Supports, "supports")]
    [InlineData(SourceRelation.Conflicts, "conflicts")]
    [InlineData(SourceRelation.Context, "context")]
    public void SpellsEveryRelation(SourceRelation relation, string expected)
    {
        Assert.Equal(expected, WireNames.ToWire(relation));
    }

    [Fact]
    public void RoundTripsEveryVerdict()
    {
        foreach (var status in Enum.GetValues<VerdictStatus>())
        {
            Assert.Equal(status, WireNames.Parse<VerdictStatus>(WireNames.ToWire(status)));
        }
    }

    [Fact]
    public void ParsesCaseInsensitivelySoAHandEditedFileStillLoads()
    {
        Assert.Equal(Jurisdiction.NgLa, WireNames.Parse<Jurisdiction>("ng-la"));
    }

    [Fact]
    public void ListsTheValidSpellingsWhenRejectingAValue()
    {
        var exception = Assert.Throws<FormatException>(
            () => WireNames.Parse<VerdictStatus>("probably_true"));

        Assert.Contains("insufficient_evidence", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nonsense")]
    public void FailsToParseAnUnknownSpelling(string? value)
    {
        Assert.False(WireNames.TryParse<VerdictStatus>(value, out _));
    }

    [Fact]
    public void NeverInventsAConfidenceVerdict()
    {
        // Safety rule 6: there is no numeric confidence anywhere, and no
        // verdict that hedges outside the four the specification allows.
        Assert.Equal(4, WireNames.AllWireNames<VerdictStatus>().Count);
    }
}
