using Ukweli.Contracts;
using Ukweli.Evidence;

namespace Ukweli.Evidence.Tests;

public class SourceCorpusTests
{
    private const string OneRecord = """
        [
          {
            "id": "ncdc-lassa-sitrep-w33-2026",
            "issuer": "Nigeria Centre for Disease Control and Prevention",
            "title": "Lassa Fever Situation Report, Week 33",
            "sourceType": "primary_official",
            "jurisdiction": "NG",
            "topic": "disease_outbreaks",
            "publishedAt": "2026-08-20",
            "checkedAt": "2026-09-01",
            "url": "https://ncdc.gov.ng/sitreps/lassa-w33-2026.pdf",
            "collectionUrl": "https://ncdc.gov.ng/diseases/sitreps",
            "excerpt": "A passage long enough to satisfy the minimum excerpt length rule.",
            "placeholder": false,
            "active": true
          }
        ]
        """;

    [Fact]
    public void ParsesTheWireSpellingOfEveryEnum()
    {
        var record = SourceCorpus.Parse(OneRecord).Single();

        // The JSON says "primary_official" and "NG", not "PrimaryOfficial" and "Ng".
        Assert.Equal(SourceType.PrimaryOfficial, record.SourceType);
        Assert.Equal(Jurisdiction.Ng, record.Jurisdiction);
        Assert.Equal(Topic.DiseaseOutbreaks, record.Topic);
    }

    [Fact]
    public void ParsesDatesAndFlags()
    {
        var record = SourceCorpus.Parse(OneRecord).Single();

        Assert.Equal(new DateOnly(2026, 8, 20), record.PublishedAt);
        Assert.Equal(new DateOnly(2026, 9, 1), record.CheckedAt);
        Assert.False(record.Placeholder);
        Assert.True(record.Active);
        Assert.True(record.IsPrimary);
    }

    [Fact]
    public void AllowsNullDatesSoAPlaceholderNeedNotCarryAnInventedOne()
    {
        var json = OneRecord
            .Replace("\"publishedAt\": \"2026-08-20\"", "\"publishedAt\": null", StringComparison.Ordinal)
            .Replace("\"checkedAt\": \"2026-09-01\"", "\"checkedAt\": null", StringComparison.Ordinal);

        var record = SourceCorpus.Parse(json).Single();

        Assert.Null(record.PublishedAt);
        Assert.Null(record.CheckedAt);
    }

    [Fact]
    public void ParsesAnEmptyArray()
    {
        Assert.Empty(SourceCorpus.Parse("[]"));
    }

    [Fact]
    public void RejectsMalformedJsonWithAMessageNamingTheFile()
    {
        var exception = Assert.Throws<SourceCorpusException>(() => SourceCorpus.Parse("{ not json"));

        Assert.Contains(SourceCorpus.FileName, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsAnUnknownEnumSpelling()
    {
        var json = OneRecord.Replace(
            "\"sourceType\": \"primary_official\"",
            "\"sourceType\": \"rumour\"",
            StringComparison.Ordinal);

        Assert.ThrowsAny<Exception>(() => SourceCorpus.Parse(json));
    }

    [Fact]
    public void ReportsAMissingFileByPath()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.json");

        var exception = Assert.Throws<SourceCorpusException>(
            () => SourceCorpus.LoadFromFile(missing));

        Assert.Contains(missing, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FindsTheCorpusBesideTheBuild()
    {
        // The build copies data/sources.json next to the assembly; if that
        // stops happening, seeding and verification both break at runtime.
        Assert.True(
            File.Exists(SourceCorpus.DefaultPath()),
            $"Expected the curated store at {SourceCorpus.DefaultPath()}.");
    }

    [Fact]
    public void NormalisationTrimsWhitespaceWithoutRewordingTheExcerpt()
    {
        var record = SourceMapping.Normalise(SourceCorpus.Parse(OneRecord).Single() with
        {
            Excerpt = "  A quoted passage.  ",
            Id = "  spaced-id  ",
        });

        Assert.Equal("A quoted passage.", record.Excerpt);
        Assert.Equal("spaced-id", record.Id);
    }

    [Fact]
    public void NormalisationTreatsABlankCollectionUrlAsAbsent()
    {
        var record = SourceMapping.Normalise(SourceCorpus.Parse(OneRecord).Single() with
        {
            CollectionUrl = "   ",
        });

        Assert.Null(record.CollectionUrl);
    }
}
