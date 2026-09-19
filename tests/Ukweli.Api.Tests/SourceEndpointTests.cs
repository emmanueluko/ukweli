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
/// Phase 1's acceptance criterion: a seeded database serves a source over HTTP,
/// and an unknown id gets a clean 404 rather than a stack trace.
/// </summary>
[Collection("database")]
public class SourceEndpointTests : IAsyncLifetime, IDisposable
{
    private const string KnownId = "test-ncdc-lassa-sitrep-w33-2026";
    private const string RetiredId = "test-retired-source";

    private readonly UkweliApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UkweliDbContext>();
        await db.Database.EnsureCreatedAsync();

        await UpsertAsync(db, new Source
        {
            Id = KnownId,
            Issuer = "Nigeria Centre for Disease Control and Prevention",
            Title = "Lassa Fever Situation Report, Week 33",
            SourceType = SourceType.PrimaryOfficial,
            Jurisdiction = Jurisdiction.Ng,
            Topic = Topic.DiseaseOutbreaks,
            PublishedAt = new DateOnly(2026, 8, 20),
            CheckedAt = new DateOnly(2026, 9, 1),
            Url = "https://ncdc.gov.ng/sitreps/lassa-w33-2026.pdf",
            CollectionUrl = "https://ncdc.gov.ng/diseases/sitreps",
            Excerpt = "A passage long enough to satisfy the minimum excerpt length rule.",
            Placeholder = false,
            Active = true,
        });

        await UpsertAsync(db, new Source
        {
            Id = RetiredId,
            Issuer = "Lagos State Government",
            Title = "A notice no longer in the corpus",
            SourceType = SourceType.SecondaryTrusted,
            Jurisdiction = Jurisdiction.NgLa,
            Topic = Topic.PaymentsLevies,
            PublishedAt = new DateOnly(2025, 1, 5),
            CheckedAt = new DateOnly(2025, 2, 1),
            Url = "https://lagosstate.gov.ng/notices/retired.pdf",
            Excerpt = "A retired passage long enough to satisfy the minimum length rule.",
            Placeholder = false,
            Active = false,
        });

        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UkweliDbContext>();

        var planted = await db.Sources
            .Where(source => source.Id == KnownId || source.Id == RetiredId)
            .ToListAsync();

        db.Sources.RemoveRange(planted);
        await db.SaveChangesAsync();
    }

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    private static async Task UpsertAsync(UkweliDbContext db, Source source)
    {
        var existing = await db.Sources.FindAsync(source.Id);
        if (existing is not null)
        {
            db.Sources.Remove(existing);
            await db.SaveChangesAsync();
        }

        db.Sources.Add(source);
    }

    [Fact]
    public async Task ServesASeededSourceOverHttp()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/sources/{KnownId}");
        var body = await response.Content.ReadFromJsonAsync<SourceResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(KnownId, body.Id);
        Assert.Equal("Nigeria Centre for Disease Control and Prevention", body.Issuer);
        Assert.Equal("https://ncdc.gov.ng/sitreps/lassa-w33-2026.pdf", body.Url);
        Assert.Equal(new DateOnly(2026, 8, 20), body.PublishedAt);
        Assert.Equal(new DateOnly(2026, 9, 1), body.CheckedAt);
        Assert.Contains("minimum excerpt length", body.Excerpt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AlwaysSaysExternalContentMayChange()
    {
        // The excerpt is what the document said when a human read it. Whatever
        // the URL serves today is a different thing, and the response says so.
        using var client = _factory.CreateClient();

        var body = await client.GetFromJsonAsync<SourceResponse>($"/api/sources/{KnownId}");

        Assert.NotNull(body);
        Assert.True(body.ExternalContentMayChange);
    }

    [Fact]
    public async Task UsesTheWireSpellingOfEveryEnum()
    {
        using var client = _factory.CreateClient();

        using var document = JsonDocument.Parse(
            await client.GetStringAsync($"/api/sources/{KnownId}"));
        var root = document.RootElement;

        Assert.Equal("primary_official", root.GetProperty("sourceType").GetString());
        Assert.Equal("NG", root.GetProperty("jurisdiction").GetString());
        Assert.Equal("disease_outbreaks", root.GetProperty("topic").GetString());
    }

    [Fact]
    public async Task StillResolvesARetiredSource()
    {
        // An analysis stored earlier cites this id; that result must stay readable.
        using var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/sources/{RetiredId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Returns404WithTheSharedErrorEnvelopeForAnUnknownId()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/sources/no-such-source");
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(ErrorCodes.NotFound, body.Error.Code);
        Assert.False(body.Error.Retryable);
        Assert.Contains("no-such-source", body.Error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Returns404RatherThanAStackTraceForAStrangeId()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/sources/%20");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListsExamplesAsAnEmptyArrayUntilPhase2()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/examples");
        var body = await response.Content.ReadFromJsonAsync<List<ExampleClaimResponse>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Empty(body);
    }
}
