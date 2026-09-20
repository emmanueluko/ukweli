using Ukweli.Contracts;
using Ukweli.Evidence;
using Ukweli.Evidence.Ingestion;

namespace Ukweli.Evidence.Tests;

/// <summary>
/// The gate that stands in for the human reader safety rule 9 originally
/// required. If these pass and the gate is still wrong, the store fills with
/// evidence nobody checked — so they are worth more scrutiny than most.
/// </summary>
public class IngestGateTests
{
    private const string Document = """
        Nigeria Centre for Disease Control and Prevention
        Lassa Fever Situation Report, 22 August 2026

        Cumulatively, 253 deaths have been reported with a Case Fatality Rate of 24.0%.
        Healthcare workers should maintain a high index of suspicion and refer cases early.
        """;

    private static readonly DateOnly Today = new(2026, 9, 20);

    private static SourceRecord Candidate(Action<Builder>? customise = null)
    {
        var builder = new Builder();
        customise?.Invoke(builder);
        return builder.Build();
    }

    // ── The check that makes fabrication impossible ──────────────────────────

    [Fact]
    public void AcceptsAPassageLiftedFromTheDocument()
    {
        var problems = IngestGate.Check(new IngestGate.Submission(Candidate(), Document, Today));

        Assert.Empty(problems);
    }

    [Fact]
    public void RejectsAnExcerptThatIsNotInTheDocument()
    {
        // The whole point: a machine may copy, never compose.
        var record = Candidate(c => c.Excerpt =
            "The agency confirmed that the outbreak has been completely eliminated nationwide.");

        var problems = IngestGate.Check(new IngestGate.Submission(record, Document, Today));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.ExcerptNotInDocument);
    }

    [Fact]
    public void RejectsAParaphraseOfSomethingTheDocumentDoesSay()
    {
        // Closest to the real failure: true in substance, not the document's
        // words. A summary presented as a quotation is still not a quotation.
        var record = Candidate(c => c.Excerpt =
            "In total 253 people have died, which is a case fatality rate of twenty-four percent.");

        var problems = IngestGate.Check(new IngestGate.Submission(record, Document, Today));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.ExcerptNotInDocument);
    }

    [Fact]
    public void IgnoresTheLineBreaksAndCurlyQuotesExtractorsIntroduce()
    {
        // A PDF extractor inserts newlines mid-sentence; that must not look
        // like the passage is absent.
        const string extracted = "Cumulatively, 253 deaths have been\n   reported with a Case\nFatality Rate of 24.0%.";

        Assert.True(IngestGate.ExcerptAppearsInDocument(
            "Cumulatively, 253 deaths have been reported with a Case Fatality Rate of 24.0%.",
            extracted));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RejectsAnEmptyExcerptOutright(string? excerpt)
    {
        Assert.False(IngestGate.ExcerptAppearsInDocument(excerpt, Document));
    }

    [Fact]
    public void RejectsAnyExcerptWhenTheDocumentCouldNotBeRead()
    {
        // An unreadable PDF yields no text. Nothing may be quoted from nothing.
        Assert.False(IngestGate.ExcerptAppearsInDocument("anything at all", string.Empty));
    }

    // ── The allowlist ────────────────────────────────────────────────────────

    [Fact]
    public void RejectsADocumentFromOutsideTheRegistry()
    {
        var record = Candidate(c => c.Url = "https://some-blog.example/what-ncdc-said.html");

        var problems = IngestGate.Check(new IngestGate.Submission(record, Document, Today));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.NotOnAllowlist);
    }

    [Theory]
    [InlineData("https://ncdc.gov.ng/themes/files/sitrep.pdf", true)]
    [InlineData("https://www.ncdc.gov.ng/news/item", true)]
    [InlineData("https://immigration.gov.ng/news/x", true)]
    [InlineData("https://firs.gov.ng/public-notices/x", false)]
    [InlineData("https://ncdc.gov.ng.evil.example/fake", false)]
    [InlineData("https://notncdc.gov.ng/fake", false)]
    [InlineData("ftp://ncdc.gov.ng/file", false)]
    [InlineData("not-a-url", false)]
    public void RecognisesOnlyRegisteredHosts(string url, bool allowed)
    {
        // The lookalike cases matter: ncdc.gov.ng.evil.example ends with a
        // registered name and must still be refused.
        Assert.Equal(allowed, SourceRegistry.IsAllowed(url));
    }

    [Fact]
    public void RejectsARecordClaimingAnIssuerTheRegistryDoesNotGiveThatHost()
    {
        // A page must not be able to describe itself as somebody else.
        var record = Candidate(c => c.Issuer = "Federal Ministry of Health");

        var problems = IngestGate.Check(new IngestGate.Submission(record, Document, Today));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.IssuerMismatch);
    }

    [Fact]
    public void RejectsANewsSiteClaimingToBePrimary()
    {
        // Safety rule 3 rests on this distinction, so it comes from the
        // registry and never from the document.
        var record = Candidate(c =>
        {
            c.Url = "https://www.premiumtimesng.com/news/story";
            c.Issuer = "Premium Times";
            c.SourceType = SourceType.PrimaryOfficial;
            c.Topic = Topic.PaymentsLevies;
        });

        var problems = IngestGate.Check(new IngestGate.Submission(record, Document, Today));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.IssuerMismatch);
    }

    [Fact]
    public void RejectsATopicThePublisherDoesNotCover()
    {
        var record = Candidate(c => c.Topic = Topic.Elections);

        var problems = IngestGate.Check(new IngestGate.Submission(record, Document, Today));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.TopicNotPublished);
    }

    // ── Dates and provenance ─────────────────────────────────────────────────

    [Fact]
    public void RejectsADateInTheFuture()
    {
        var record = Candidate(c => c.PublishedAt = Today.AddDays(2));

        var problems = IngestGate.Check(new IngestGate.Submission(record, Document, Today));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.ImplausibleDate);
    }

    [Fact]
    public void RejectsARecordWithNoDateAtAll()
    {
        var record = Candidate(c => c.PublishedAt = null);

        var problems = IngestGate.Check(new IngestGate.Submission(record, Document, Today));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.ImplausibleDate);
    }

    [Fact]
    public void RejectsAnIngestedRecordThatClaimsToBeHumanCurated()
    {
        // What nobody read must stay distinguishable from what somebody did.
        var record = Candidate(c => c.Curation = Curation.Human);

        var problems = IngestGate.Check(new IngestGate.Submission(record, Document, Today));

        Assert.Contains(problems, p => p.Code == SourceProblemCodes.WrongCuration);
    }

    // ── The registry itself ──────────────────────────────────────────────────

    [Fact]
    public void EveryNewsPublisherIsSecondary()
    {
        // If one were registered as primary it could carry a verdict alone,
        // which is exactly what rule 5 forbids.
        Assert.All(SourceRegistry.Reporting, publisher =>
            Assert.Equal(SourceType.SecondaryTrusted, publisher.SourceType));
    }

    [Fact]
    public void EveryOfficialPublisherIsPrimary()
    {
        Assert.All(SourceRegistry.Official, publisher =>
            Assert.Equal(SourceType.PrimaryOfficial, publisher.SourceType));
    }

    [Fact]
    public void EveryPublisherNamesAtLeastOneTopicAndOneCollection()
    {
        Assert.All(SourceRegistry.All, publisher =>
        {
            Assert.NotEmpty(publisher.Topics);
            Assert.NotEmpty(publisher.Collections);
            Assert.All(publisher.Collections, collection =>
                Assert.True(SourceValidator.IsHttpUrl(collection), $"{collection} is not a URL."));
        });
    }

    [Fact]
    public void EveryTopicHasAPublisherThatCoversIt()
    {
        // A topic with no publisher answers every claim with
        // insufficient_evidence, which is honest but useless.
        var covered = SourceRegistry.All.SelectMany(publisher => publisher.Topics).Distinct();

        Assert.Empty(Enum.GetValues<Topic>().Except(covered));
    }

    internal sealed class Builder
    {
        public string Id { get; set; } = "ncdc-disease-outbreaks-20260822-12345";
        public string Issuer { get; set; } = "Nigeria Centre for Disease Control and Prevention";
        public string Title { get; set; } = "Lassa Fever Situation Report, 22 August 2026";
        public SourceType SourceType { get; set; } = SourceType.PrimaryOfficial;
        public Jurisdiction Jurisdiction { get; set; } = Jurisdiction.Ng;
        public Topic Topic { get; set; } = Topic.DiseaseOutbreaks;
        public DateOnly? PublishedAt { get; set; } = new(2026, 8, 22);
        public string Url { get; set; } = "https://ncdc.gov.ng/themes/files/sitrep-w34.pdf";
        public string Excerpt { get; set; } =
            "Cumulatively, 253 deaths have been reported with a Case Fatality Rate of 24.0%.";
        public Curation Curation { get; set; } = Curation.Automated;

        public SourceRecord Build() => new()
        {
            Id = Id,
            Issuer = Issuer,
            Title = Title,
            SourceType = SourceType,
            Jurisdiction = Jurisdiction,
            Topic = Topic,
            PublishedAt = PublishedAt,
            CheckedAt = new DateOnly(2026, 9, 20),
            Url = Url,
            CollectionUrl = "https://ncdc.gov.ng/diseases/sitreps",
            Excerpt = Excerpt,
            Placeholder = false,
            Active = true,
            Curation = Curation,
        };
    }
}
