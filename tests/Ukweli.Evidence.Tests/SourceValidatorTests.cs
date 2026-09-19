using Ukweli.Contracts;
using Ukweli.Evidence;

namespace Ukweli.Evidence.Tests;

/// <summary>
/// Safety rule 9 and the shape rules around it. The placeholder gate is the
/// one that matters most: it is what stops uncurated evidence reaching a demo.
/// </summary>
public class SourceValidatorTests
{
    /// <summary>A fully curated record, used as the baseline each test perturbs.</summary>
    private static SourceRecord Valid(Action<SourceRecordBuilder>? customise = null)
    {
        var builder = new SourceRecordBuilder();
        customise?.Invoke(builder);
        return builder.Build();
    }

    // ── Safety rule 9: placeholders must fail ────────────────────────────────

    [Fact]
    public void RejectsARecordFlaggedAsAPlaceholder()
    {
        var problems = SourceValidator.ValidateRecord(Valid(r => r.Placeholder = true));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.Placeholder);
    }

    [Theory]
    [InlineData("excerpt")]
    [InlineData("url")]
    [InlineData("title")]
    [InlineData("id")]
    [InlineData("issuer")]
    public void RejectsARecordWhoseFieldStillCarriesTheMarker(string field)
    {
        // Flipping "placeholder": false while leaving the text unwritten must
        // not get a record past the gate.
        var record = Valid(r =>
        {
            r.Placeholder = false;
            switch (field)
            {
                case "excerpt": r.Excerpt = "PLACEHOLDER — curator must replace with verbatim excerpt"; break;
                case "url": r.Url = "PLACEHOLDER — curator must replace with the specific URL"; break;
                case "title": r.Title = "PLACEHOLDER — curator must replace"; break;
                case "id": r.Id = "ncdc-sitrep-PLACEHOLDER"; break;
                case "issuer": r.Issuer = "PLACEHOLDER"; break;
                default: throw new ArgumentOutOfRangeException(nameof(field), field, null);
            }
        });

        var problems = SourceValidator.ValidateRecord(record);

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.Placeholder);
    }

    [Fact]
    public void ReportsOnlyThePlaceholderProblemForAnUncuratedRecord()
    {
        // Every other complaint about an uncurated record is noise a curator
        // would have to read past.
        var problems = SourceValidator.ValidateRecord(Valid(r =>
        {
            r.Placeholder = true;
            r.Url = "not-a-url";
            r.PublishedAt = null;
            r.CheckedAt = null;
        }));

        Assert.Single(problems);
        Assert.Equal(SourceProblemCodes.Placeholder, problems[0].Code);
    }

    [Fact]
    public void TheShippedCorpusIsCuratedAndValid()
    {
        // This test used to assert the opposite — that every record was still a
        // placeholder — as a guard against anyone quieting a failing build by
        // inventing evidence. The corpus has since been curated from the real
        // NCDC situation reports, so it now asserts what must stay true: every
        // record is real, and the whole store passes validation.
        var records = SourceMapping.Normalise(SourceCorpus.LoadFromFile(SourceCorpus.DefaultPath()));

        Assert.NotEmpty(records);
        Assert.All(records, record => Assert.False(
            SourceValidator.LooksUncurated(record),
            $"'{record.Id}' is not curated."));
        Assert.Empty(SourceValidator.Validate(records));
    }

    [Fact]
    public void EveryShippedSourceCitesASpecificDocument()
    {
        var records = SourceCorpus.LoadFromFile(SourceCorpus.DefaultPath());

        // A verdict citing an index cites nothing in particular.
        Assert.All(records, record =>
        {
            Assert.True(SourceValidator.IsHttpUrl(record.Url), $"'{record.Id}' has no document URL.");
            Assert.NotEqual(record.Url.TrimEnd('/'), record.CollectionUrl?.TrimEnd('/'));
        });
    }

    [Fact]
    public void AcceptsAFullyCuratedRecord()
    {
        Assert.Empty(SourceValidator.ValidateRecord(Valid()));
    }

    // ── Duplicate ids ────────────────────────────────────────────────────────

    [Fact]
    public void RejectsDuplicateIds()
    {
        var problems = SourceValidator.Validate([
            Valid(r => r.Id = "ncdc-lassa-sitrep-w33-2026"),
            Valid(r => r.Id = "ncdc-lassa-sitrep-w33-2026"),
        ]);

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.DuplicateId);
    }

    [Fact]
    public void TreatsIdsDifferingOnlyByCaseAsDuplicates()
    {
        var problems = SourceValidator.Validate([
            Valid(r => r.Id = "ncdc-lassa-sitrep-w33-2026"),
            Valid(r => r.Id = "NCDC-Lassa-Sitrep-W33-2026"),
        ]);

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.DuplicateId);
    }

    [Fact]
    public void AcceptsDistinctIds()
    {
        var problems = SourceValidator.Validate([
            Valid(r => r.Id = "ncdc-lassa-sitrep-w33-2026"),
            Valid(r => r.Id = "lagos-land-use-charge-2026"),
        ]);

        Assert.DoesNotContain(problems, p => p.Code == SourceProblemCodes.DuplicateId);
    }

    // ── Ids, URLs, dates, excerpts ───────────────────────────────────────────

    [Theory]
    [InlineData("Not A Slug")]
    [InlineData("under_scores")]
    [InlineData("Trailing-")]
    [InlineData("double--hyphen")]
    public void RejectsAnIdThatIsNotAReadableSlug(string id)
    {
        var problems = SourceValidator.ValidateRecord(Valid(r => r.Id = id));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.InvalidId);
    }

    [Theory]
    [InlineData("ncdc-lassa-sitrep-w33-2026")]
    [InlineData("lagos-land-use-charge-2026")]
    [InlineData("a1")]
    public void AcceptsAReadableSlug(string id)
    {
        var problems = SourceValidator.ValidateRecord(Valid(r => r.Id = id));

        Assert.DoesNotContain(problems, p => p.Code == SourceProblemCodes.InvalidId);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.gov.ng/doc.pdf")]
    [InlineData("/relative/path.pdf")]
    public void RejectsAUrlThatIsNotAbsoluteHttp(string url)
    {
        var problems = SourceValidator.ValidateRecord(Valid(r => r.Url = url));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.InvalidUrl);
    }

    [Fact]
    public void RejectsAUrlThatIsJustTheIndexPage()
    {
        // A verdict citing an index cites nothing in particular.
        var problems = SourceValidator.ValidateRecord(Valid(r =>
        {
            r.Url = "https://ncdc.gov.ng/diseases/sitreps";
            r.CollectionUrl = "https://ncdc.gov.ng/diseases/sitreps";
        }));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.UrlIsIndexPage);
    }

    [Fact]
    public void RejectsAnIndexUrlThatDiffersOnlyByATrailingSlash()
    {
        var problems = SourceValidator.ValidateRecord(Valid(r =>
        {
            r.Url = "https://ncdc.gov.ng/diseases/sitreps/";
            r.CollectionUrl = "https://ncdc.gov.ng/diseases/sitreps";
        }));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.UrlIsIndexPage);
    }

    [Fact]
    public void RequiresBothDates()
    {
        var problems = SourceValidator.ValidateRecord(Valid(r =>
        {
            r.PublishedAt = null;
            r.CheckedAt = null;
        }));

        Assert.Equal(2, problems.Count(p => p.Code == SourceProblemCodes.MissingDate));
    }

    [Fact]
    public void RejectsARecordCheckedBeforeItWasPublished()
    {
        var problems = SourceValidator.ValidateRecord(Valid(r =>
        {
            r.PublishedAt = new DateOnly(2026, 8, 20);
            r.CheckedAt = new DateOnly(2026, 8, 1);
        }));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.DatesOutOfOrder);
    }

    [Fact]
    public void RejectsAnExcerptTooShortToCarryAVerdict()
    {
        var problems = SourceValidator.ValidateRecord(Valid(r => r.Excerpt = "Too short."));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.ExcerptTooShort);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsABlankRequiredField(string blank)
    {
        var problems = SourceValidator.ValidateRecord(Valid(r => r.Issuer = blank));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.MissingField);
    }

    // ── Corpus-level rules ───────────────────────────────────────────────────

    [Fact]
    public void RejectsAnEmptyCorpus()
    {
        var problems = SourceValidator.Validate([]);

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.CorpusEmpty);
    }

    [Fact]
    public void RejectsACorpusLargerThanTheCap()
    {
        // Retrieval is keyword scoring over this list, not search.
        var records = Enumerable
            .Range(0, SourceCorpus.MaxRecords + 1)
            .Select(i => Valid(r => r.Id = $"source-{i}"))
            .ToArray();

        Assert.Contains(
            SourceValidator.Validate(records),
            p => p.Code == SourceProblemCodes.CorpusTooLarge);
    }

    [Fact]
    public void AcceptsACorpusExactlyAtTheCap()
    {
        var records = Enumerable
            .Range(0, SourceCorpus.MaxRecords)
            .Select(i => Valid(r => r.Id = $"source-{i}"))
            .ToArray();

        Assert.DoesNotContain(
            SourceValidator.Validate(records),
            p => p.Code == SourceProblemCodes.CorpusTooLarge);
    }

    internal sealed class SourceRecordBuilder
    {
        public string Id { get; set; } = "ncdc-lassa-sitrep-w33-2026";
        public string Issuer { get; set; } = "Nigeria Centre for Disease Control and Prevention";
        public string Title { get; set; } = "Lassa Fever Situation Report, Week 33";
        public SourceType SourceType { get; set; } = SourceType.PrimaryOfficial;
        public Jurisdiction Jurisdiction { get; set; } = Jurisdiction.Ng;
        public Topic Topic { get; set; } = Topic.DiseaseOutbreaks;
        public DateOnly? PublishedAt { get; set; } = new(2026, 8, 20);
        public DateOnly? CheckedAt { get; set; } = new(2026, 9, 1);
        public string Url { get; set; } = "https://ncdc.gov.ng/sitreps/lassa-w33-2026.pdf";
        public string? CollectionUrl { get; set; } = "https://ncdc.gov.ng/diseases/sitreps";
        public string Excerpt { get; set; } =
            "A sample excerpt long enough to satisfy the minimum length rule for a passage.";
        public bool Placeholder { get; set; }
        public bool Active { get; set; } = true;

        public SourceRecord Build() => new()
        {
            Id = Id,
            Issuer = Issuer,
            Title = Title,
            SourceType = SourceType,
            Jurisdiction = Jurisdiction,
            Topic = Topic,
            PublishedAt = PublishedAt,
            CheckedAt = CheckedAt,
            Url = Url,
            CollectionUrl = CollectionUrl,
            Excerpt = Excerpt,
            Placeholder = Placeholder,
            Active = Active,
        };
    }
}
