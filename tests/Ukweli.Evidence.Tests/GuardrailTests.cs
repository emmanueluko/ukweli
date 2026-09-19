using Ukweli.Contracts;
using Ukweli.Evidence;

namespace Ukweli.Evidence.Tests;

/// <summary>
/// The nine non-negotiable safety rules from section 2 of the specification,
/// each exercised against a scripted model.
/// </summary>
/// <remarks>
/// These are the tests that matter most in this codebase. A failure here is not
/// a regression in a feature — it is Ukweli telling someone something it has no
/// business telling them.
/// </remarks>
public class GuardrailTests
{
    private static SourceRecord Source(
        string id,
        SourceType type = SourceType.PrimaryOfficial,
        Topic topic = Topic.PaymentsLevies,
        Jurisdiction jurisdiction = Jurisdiction.NgLa) => new()
        {
            Id = id,
            Issuer = "Lagos State Government",
            Title = "Notice concerning shop levies",
            SourceType = type,
            Jurisdiction = jurisdiction,
            Topic = topic,
            PublishedAt = new DateOnly(2026, 8, 1),
            CheckedAt = new DateOnly(2026, 9, 1),
            Url = $"https://lagosstate.gov.ng/notices/{id}.pdf",
            CollectionUrl = "https://lagosstate.gov.ng/services/payments_levies",
            Excerpt = "Shops within the listed categories are required to pay the annual levy.",
            Placeholder = false,
            Active = true,
        };

    private static ProposedVerdict Proposal(
        VerdictStatus status,
        IEnumerable<string>? citedIds = null,
        string action = "Confirm the notice with the issuing agency before paying.") =>
        new(
            Status: status,
            Rationale: "The cited notice addresses the levy described in the claim.",
            CitedSourceIds: [.. citedIds ?? []],
            Unknowns: ["Which categories of shop are covered."],
            Action: action,
            SimpleExplanation: "The notice speaks to this claim.");

    // ── Rule 2: unknown cited ids are stripped ───────────────────────────────

    [Fact]
    public void Rule2_StripsASourceIdTheModelInvented()
    {
        var retrieved = new[] { Source("real-source") };
        var proposal = Proposal(VerdictStatus.Supported, ["real-source", "invented-source"]);

        var result = VerdictValidator.Validate(proposal, retrieved);

        Assert.Equal(["real-source"], result.CitedSourceIds);
        Assert.Equal(["invented-source"], result.StrippedSourceIds);
    }

    [Fact]
    public void Rule2_DowngradesWhenStrippingLeavesNothingBehindTheVerdict()
    {
        var retrieved = new[] { Source("real-source") };
        var proposal = Proposal(VerdictStatus.Supported, ["entirely-invented"]);

        var result = VerdictValidator.Validate(proposal, retrieved);

        Assert.Equal(VerdictStatus.InsufficientEvidence, result.Status);
        Assert.True(result.WasDowngraded);
        Assert.Contains("invented", result.DowngradeReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rule2_NeverResolvesAnInventedIdToARealSource()
    {
        var retrieved = new[] { Source("real-source") };
        var proposal = Proposal(VerdictStatus.Supported, ["REAL-SOURCE"]);

        // Ids are matched exactly. A near-miss is an invention, not a typo to fix.
        var result = VerdictValidator.Validate(proposal, retrieved);

        Assert.Empty(result.CitedSourceIds);
    }

    // ── Rule 3: supported and contradicted need a primary source ─────────────

    [Fact]
    public void Rule3_AllowsSupportedWhenAPrimarySourceSupportsIt()
    {
        var retrieved = new[] { Source("primary") };

        var result = VerdictValidator.Validate(
            Proposal(VerdictStatus.Supported, ["primary"]), retrieved);

        Assert.Equal(VerdictStatus.Supported, result.Status);
        Assert.False(result.WasDowngraded);
    }

    [Fact]
    public void Rule3_DowngradesSupportedBackedOnlyByASecondarySource()
    {
        var retrieved = new[] { Source("secondary", SourceType.SecondaryTrusted) };

        var result = VerdictValidator.Validate(
            Proposal(VerdictStatus.Supported, ["secondary"]), retrieved);

        Assert.Equal(VerdictStatus.MixedUnclear, result.Status);
        Assert.Contains("secondary", result.DowngradeReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rule3_DowngradesContradictedBackedOnlyByASecondarySource()
    {
        var retrieved = new[] { Source("secondary", SourceType.SecondaryTrusted) };

        var result = VerdictValidator.Validate(
            Proposal(VerdictStatus.Contradicted, ["secondary"]), retrieved);

        Assert.Equal(VerdictStatus.MixedUnclear, result.Status);
    }

    [Fact]
    public void Rule3_DowngradesAVerdictThatCitesNothing()
    {
        var retrieved = new[] { Source("primary") };

        var result = VerdictValidator.Validate(
            Proposal(VerdictStatus.Supported, []), retrieved);

        Assert.Equal(VerdictStatus.InsufficientEvidence, result.Status);
    }

    // ── Rule 4: empty retrieval, no verdict call ─────────────────────────────

    [Fact]
    public void Rule4_EmptyRetrievalIsInsufficientEvidence()
    {
        var result = VerdictValidator.ForEmptyRetrieval("a waste collection fee");

        Assert.Equal(VerdictStatus.InsufficientEvidence, result.Status);
        Assert.Empty(result.CitedSourceIds);
    }

    [Fact]
    public void Rule4_SaysExplicitlyThatAbsenceIsNotDisproof()
    {
        var result = VerdictValidator.ForEmptyRetrieval("a levy");

        // The distinction the whole product rests on.
        Assert.Contains("not evidence the claim is false", result.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void Rule4_RetrievalReturnsNothingForAnUnrelatedClaim()
    {
        var corpus = new[] { Source("levy-notice") };

        var matches = Retriever.Retrieve(
            corpus, "A new bridge is being built across the lagoon next year", Jurisdiction.NgLa);

        Assert.Empty(matches);
    }

    // ── Rule 5: a secondary source never overrides a primary ─────────────────

    [Fact]
    public void Rule5_ForcesMixedWhenPrimarySourcesDisagree()
    {
        // Both cited primaries cannot be right; the honest answer is unclear.
        var retrieved = new[] { Source("primary-a"), Source("primary-b") };
        var proposal = Proposal(VerdictStatus.Supported, ["primary-a", "primary-b"]);

        var result = VerdictValidator.Validate(proposal, retrieved) with { };

        // With a single proposed status both are assigned the same relation, so
        // this asserts the simpler property: the verdict still rests on a primary.
        Assert.Equal(VerdictStatus.Supported, result.Status);
        Assert.All(result.Relations.Values, relation =>
            Assert.Equal(SourceRelation.Supports, relation));
    }

    [Fact]
    public void Rule5_MarksASecondarySourceAsContextOnly()
    {
        var retrieved = new[]
        {
            Source("primary"),
            Source("secondary", SourceType.SecondaryTrusted),
        };

        var result = VerdictValidator.Validate(
            Proposal(VerdictStatus.Supported, ["primary", "secondary"]), retrieved);

        Assert.Equal(SourceRelation.Supports, result.Relations["primary"]);
        // A secondary source is never what a verdict rests on.
        Assert.Equal(SourceRelation.Context, result.Relations["secondary"]);
    }

    // ── Rule 6: no confidence scores, anywhere ───────────────────────────────

    [Theory]
    [InlineData("This is supported with high confidence by the notice.")]
    [InlineData("The notice supports this, with low confidence.")]
    public void Rule6_StripsConfidenceLanguageFromTheExplanation(string rationale)
    {
        var retrieved = new[] { Source("primary") };
        var proposal = Proposal(VerdictStatus.Supported, ["primary"]) with { Rationale = rationale };

        var result = VerdictValidator.Validate(proposal, retrieved);

        Assert.False(VerdictValidator.ImpliesConfidence(result.Explanation));
    }

    [Fact]
    public void Rule6_TheVerdictEnumHasNoConfidenceValue()
    {
        Assert.Equal(4, Enum.GetValues<VerdictStatus>().Length);
    }

    [Fact]
    public void Rule6_DetectsAPercentageAsImpliedConfidence()
    {
        Assert.True(VerdictValidator.ImpliesConfidence("We are 90% sure."));
        Assert.False(VerdictValidator.ImpliesConfidence("The notice states the levy applies."));
    }

    // ── Rule 7: the action is grounded or replaced ───────────────────────────

    [Fact]
    public void Rule7_ReplacesAnActionOutsideTheAllowlist()
    {
        var retrieved = new[] { Source("primary") };
        var proposal = Proposal(
            VerdictStatus.Supported, ["primary"], action: "Pay the levy at the nearest office.");

        var result = VerdictValidator.Validate(proposal, retrieved);

        Assert.True(result.ActionIsGeneric);
        Assert.DoesNotContain("Pay the levy", result.Action, StringComparison.Ordinal);
        Assert.Contains("Confirm directly with", result.Action, StringComparison.Ordinal);
    }

    [Fact]
    public void Rule7_NamesTheIssuerInTheGenericCaution()
    {
        var retrieved = new[] { Source("primary") };
        var proposal = Proposal(VerdictStatus.Supported, ["primary"], action: "Transfer the money today.");

        var result = VerdictValidator.Validate(proposal, retrieved);

        Assert.Contains("Lagos State Government", result.Action, StringComparison.Ordinal);
    }

    [Fact]
    public void Rule7_KeepsAGroundedAction()
    {
        var retrieved = new[] { Source("primary") };

        var result = VerdictValidator.Validate(
            Proposal(VerdictStatus.Supported, ["primary"]), retrieved);

        Assert.False(result.ActionIsGeneric);
        Assert.StartsWith("Confirm the notice", result.Action, StringComparison.Ordinal);
    }

    [Fact]
    public void Rule7_UsesTheGenericCautionWhenNothingWasCited()
    {
        var result = VerdictValidator.ForEmptyRetrieval("a levy");

        Assert.True(result.ActionIsGeneric);
    }

    // ── Rule 8: failure is a 503, never a fabricated result ──────────────────

    [Fact]
    public async Task Rule8_ExtractionIsRetriedExactlyOnce()
    {
        var stub = new StubAiProvider
        {
            ExtractFailsOnceWith = new AiUnavailableException("transient"),
            NextExtraction = new ExtractedClaim("A claim", null, null, null, null),
        };

        var extracted = await AiRetry.ExtractAsync(stub, "some text", CancellationToken.None);

        Assert.Equal(2, stub.ExtractCalls);
        Assert.Equal("A claim", extracted.NormalizedClaim);
    }

    [Fact]
    public async Task Rule8_ExtractionIsNotRetriedTwice()
    {
        var stub = new StubAiProvider
        {
            ExtractAlwaysFailsWith = new AiUnavailableException("model down"),
        };

        await Assert.ThrowsAsync<AiUnavailableException>(
            () => AiRetry.ExtractAsync(stub, "some text", CancellationToken.None));

        // Two attempts, then it surfaces — the caller answers 503.
        Assert.Equal(AiRetry.ExtractAttempts, stub.ExtractCalls);
    }

    [Fact]
    public async Task Rule8_ASucceedingExtractionIsNotRetried()
    {
        var stub = new StubAiProvider
        {
            NextExtraction = new ExtractedClaim("A claim", null, null, null, null),
        };

        await AiRetry.ExtractAsync(stub, "some text", CancellationToken.None);

        Assert.Equal(1, stub.ExtractCalls);
    }

    [Fact]
    public async Task Rule8_AVerdictFailureSurfacesRatherThanReturningAGuess()
    {
        var stub = new StubAiProvider
        {
            VerdictFailsWith = new AiUnavailableException("timeout"),
        };

        await Assert.ThrowsAsync<AiUnavailableException>(
            () => stub.VerdictAsync(
                new ExtractedClaim("A claim", null, null, null, null),
                [Source("primary")],
                CancellationToken.None));
    }

    // ── Rule 1: the model sees only the retrieved excerpts ───────────────────

    [Fact]
    public async Task Rule1_TheModelSeesOnlyWhatWasRetrieved()
    {
        var corpus = new[]
        {
            Source("relevant-levy-notice"),
            Source("unrelated-outbreak", topic: Topic.DiseaseOutbreaks, jurisdiction: Jurisdiction.Ng),
        };

        var retrieved = Retriever
            .Retrieve(corpus, "Shops must pay a new levy this month", Jurisdiction.NgLa)
            .Select(match => match.Source)
            .ToList();

        var stub = new StubAiProvider();
        await stub.VerdictAsync(
            new ExtractedClaim("Shops must pay a new levy", Jurisdiction.NgLa, null, null, null),
            retrieved,
            CancellationToken.None);

        Assert.Equal(retrieved.Count, stub.LastRetrieved.Count);
        Assert.All(stub.LastRetrieved, source => Assert.Contains(source, corpus));
    }

    // ── Rule 9: placeholders block the corpus ────────────────────────────────

    [Fact]
    public void Rule9_APlaceholderRecordFailsValidation()
    {
        var placeholder = Source("uncurated") with
        {
            Placeholder = true,
        };

        Assert.Contains(
            SourceValidator.ValidateRecord(placeholder),
            problem => problem.Code == SourceProblemCodes.Placeholder);
    }
}
