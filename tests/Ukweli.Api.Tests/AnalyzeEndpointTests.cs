using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ukweli.Contracts;
using Ukweli.Data;
using Ukweli.Data.Entities;

namespace Ukweli.Api.Tests;

/// <summary>
/// Phase 2's acceptance criterion: all three seeded claims return the right
/// verdict over HTTP, and a result reloads by id. No AI is involved anywhere in
/// this file.
/// </summary>
[Collection("database")]
public class AnalyzeEndpointTests : IAsyncLifetime, IDisposable
{
    private const string SupportedSeed = "seed-lagos-shop-levy";
    private const string ContradictedSeed = "seed-lassa-cure-rumour";
    private const string InsufficientSeed = "seed-unlisted-local-fee";

    private readonly UkweliApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UkweliDbContext>();

        // Seeded analyses are created on first use, so clear any left from an
        // earlier run to keep each test independent.
        await ClearSeededAsync(db);
        await SeedSourcesAsync(db);
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await ClearSeededAsync(scope.ServiceProvider.GetRequiredService<UkweliDbContext>());
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task ClearSeededAsync(UkweliDbContext db)
    {
        var seeded = await db.ClaimAnalyses.Where(a => a.IsSeeded).ToListAsync();
        db.ClaimAnalyses.RemoveRange(seeded);
        await db.SaveChangesAsync();
    }

    /// <summary>The seeds cite these ids, so the sources must exist to resolve.</summary>
    private static async Task SeedSourcesAsync(UkweliDbContext db)
    {
        var required = new[]
        {
            ("lagos-levy-notice-PLACEHOLDER", "Lagos State Government", Jurisdiction.NgLa, Topic.PaymentsLevies),
            ("ncdc-sitrep-PLACEHOLDER", "Nigeria Centre for Disease Control and Prevention", Jurisdiction.Ng, Topic.DiseaseOutbreaks),
        };

        foreach (var (id, issuer, jurisdiction, topic) in required)
        {
            if (await db.Sources.AnyAsync(source => source.Id == id))
            {
                continue;
            }

            db.Sources.Add(new Source
            {
                Id = id,
                Issuer = issuer,
                Title = "PLACEHOLDER",
                SourceType = SourceType.PrimaryOfficial,
                Jurisdiction = jurisdiction,
                Topic = topic,
                Url = "PLACEHOLDER",
                Excerpt = "PLACEHOLDER — curator must replace with verbatim excerpt",
                Placeholder = true,
                Active = true,
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<AnalysisResponse> AnalyzeExampleAsync(HttpClient client, string exampleId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/analyze", new AnalyzeRequest(null, exampleId));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AnalysisResponse>();
        Assert.NotNull(body);
        return body;
    }

    // ── The three seeded verdicts ────────────────────────────────────────────

    [Fact]
    public async Task ReturnsSupportedForTheSupportedSeed()
    {
        using var client = _factory.CreateClient();

        var body = await AnalyzeExampleAsync(client, SupportedSeed);

        Assert.Equal(VerdictStatus.Supported, body.Status);
        Assert.NotEmpty(body.Sources);
        Assert.Contains(body.Sources, source => source.Relation == SourceRelation.Supports);
    }

    [Fact]
    public async Task ReturnsContradictedForTheContradictedSeed()
    {
        using var client = _factory.CreateClient();

        var body = await AnalyzeExampleAsync(client, ContradictedSeed);

        Assert.Equal(VerdictStatus.Contradicted, body.Status);
        Assert.Contains(body.Sources, source => source.Relation == SourceRelation.Conflicts);
    }

    [Fact]
    public async Task ReturnsInsufficientEvidenceForTheUnlistedSeed()
    {
        using var client = _factory.CreateClient();

        var body = await AnalyzeExampleAsync(client, InsufficientSeed);

        Assert.Equal(VerdictStatus.InsufficientEvidence, body.Status);
        // Absence of evidence is never dressed up as disproof.
        Assert.Empty(body.Sources);
        Assert.Null(body.CheckedOn);
        Assert.NotEmpty(body.Unknowns);
    }

    [Fact]
    public async Task MatchesASeedByItsClaimTextAsWellAsItsId()
    {
        using var client = _factory.CreateClient();

        const string pasted =
            "A voice note is going round saying the health authorities have confirmed a "
            + "herbal mixture cures Lassa fever and no one needs to go to hospital.";

        var response = await client.PostAsJsonAsync("/api/analyze", new AnalyzeRequest(pasted, null));
        var body = await response.Content.ReadFromJsonAsync<AnalysisResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(VerdictStatus.Contradicted, body.Status);
    }

    // ── Storage and sharing ──────────────────────────────────────────────────

    [Fact]
    public async Task ReloadsAStoredResultById()
    {
        using var client = _factory.CreateClient();

        var analyzed = await AnalyzeExampleAsync(client, SupportedSeed);
        var reloaded = await client.GetFromJsonAsync<AnalysisResponse>(
            $"/api/results/{analyzed.Id}");

        Assert.NotNull(reloaded);
        Assert.Equal(analyzed.Id, reloaded.Id);
        Assert.Equal(analyzed.Status, reloaded.Status);
        Assert.Equal(analyzed.ShareText, reloaded.ShareText);
    }

    [Fact]
    public async Task ShareTextNeverContainsTheUsersInput()
    {
        using var client = _factory.CreateClient();

        const string pasted =
            "A voice note is going round saying the health authorities have confirmed a "
            + "herbal mixture cures Lassa fever and no one needs to go to hospital.";

        var response = await client.PostAsJsonAsync("/api/analyze", new AnalyzeRequest(pasted, null));
        var body = await response.Content.ReadFromJsonAsync<AnalysisResponse>();

        Assert.NotNull(body);
        Assert.DoesNotContain("herbal", body.ShareText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("voice note", body.ShareText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(body.NormalizedClaim, body.ShareText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StoresOnlyTheNormalisedClaimAndNeverTheRawInput()
    {
        using var client = _factory.CreateClient();

        const string decorated =
            "  ➡️ A voice note is going round saying the health authorities have confirmed a "
            + "herbal mixture cures Lassa fever and no one needs to go to hospital. 😱  ";

        var response = await client.PostAsJsonAsync(
            "/api/analyze", new AnalyzeRequest(decorated, null));
        var body = await response.Content.ReadFromJsonAsync<AnalysisResponse>();

        Assert.NotNull(body);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UkweliDbContext>();
        var stored = await db.ClaimAnalyses.FirstAsync(a => a.Id == body.Id);

        Assert.DoesNotContain("➡️", stored.NormalizedClaim, StringComparison.Ordinal);
        Assert.NotEqual(decorated, stored.NormalizedClaim);
        Assert.True(stored.IsSeeded);
        Assert.Equal(AnalyzeService.SeededModelVersion, stored.ModelVersion);
    }

    [Fact]
    public async Task ReusesTheStoredAnalysisForARepeatedSeed()
    {
        using var client = _factory.CreateClient();

        var first = await AnalyzeExampleAsync(client, SupportedSeed);
        var second = await AnalyzeExampleAsync(client, SupportedSeed);

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task NeverReturnsAConfidenceScore()
    {
        using var client = _factory.CreateClient();

        var analyzed = await AnalyzeExampleAsync(client, SupportedSeed);
        var raw = await client.GetStringAsync($"/api/results/{analyzed.Id}");

        using var document = JsonDocument.Parse(raw);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            Assert.DoesNotContain("confidence", property.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("score", property.Name, StringComparison.OrdinalIgnoreCase);
        }
    }

    // ── Errors ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Returns400WithAHelpfulMessageForShortInput()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyze", new AnalyzeRequest("too short", null));
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(ErrorCodes.ValidationFailed, body.Error.Code);
        Assert.False(body.Error.Retryable);
        Assert.Contains("at least 20", body.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Returns404ForAnUnknownResultId()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/results/aaaaaaaaaa");
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(ErrorCodes.NotFound, body.Error.Code);
    }

    [Fact]
    public async Task Returns404RatherThanQueryingForAMalformedResultId()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/results/not-an-id-at-all");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListsTheSeededClaimsAsExamples()
    {
        using var client = _factory.CreateClient();

        var examples = await client.GetFromJsonAsync<List<ExampleClaimResponse>>("/api/examples");

        Assert.NotNull(examples);
        Assert.Equal(3, examples.Count);
        Assert.Contains(examples, example => example.Id == SupportedSeed);
        Assert.All(examples, example => Assert.False(string.IsNullOrWhiteSpace(example.Claim)));
    }
}
