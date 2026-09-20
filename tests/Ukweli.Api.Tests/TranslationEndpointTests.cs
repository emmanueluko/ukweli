using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ukweli.Contracts;
using Ukweli.Data;
using Ukweli.Data.Entities;

namespace Ukweli.Api.Tests;

/// <summary>
/// Reading the same result in another language over HTTP.
/// </summary>
/// <remarks>
/// The property under test throughout: switching language changes how the
/// answer reads and never what it says. Same verdict, same sources, same dates,
/// same relations — and excerpts quoted exactly as the authority published
/// them, because a translated quotation is no longer a quotation.
/// </remarks>
[Collection("database")]
public class TranslationEndpointTests : IAsyncLifetime, IDisposable
{
    private const string SupportedSeed = "seed-lassa-death-toll";

    private readonly UkweliApiFactory _factory = new();

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UkweliDbContext>();

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

    [Theory]
    [InlineData("pcm")]
    [InlineData("fr")]
    public async Task ASeededResultReadsInTheRequestedLanguage(string lang)
    {
        using var client = _factory.CreateClient();

        var english = await AnalyzeAsync(client, lang: null);
        var translated = await GetResultAsync(client, english.Id, lang);

        // The finding is the same one.
        Assert.Equal(english.Status, translated.Status);
        Assert.Equal(english.CheckedOn, translated.CheckedOn);
        Assert.Equal(
            english.Sources.Select(s => (s.Id, s.Relation)),
            translated.Sources.Select(s => (s.Id, s.Relation)));

        // The prose is not.
        Assert.Equal(WireNames.TryParse<Language>(lang, out var expected) ? expected : Language.English,
            translated.Language);
        Assert.Null(translated.TranslationNote);
        Assert.NotEqual(english.Explanation, translated.Explanation);
        Assert.Equal(english.Unknowns.Count, translated.Unknowns.Count);
    }

    [Theory]
    [InlineData("pcm")]
    [InlineData("fr")]
    public async Task ExcerptsAreNeverTranslated(string lang)
    {
        using var client = _factory.CreateClient();

        var english = await AnalyzeAsync(client, lang: null);
        var translated = await GetResultAsync(client, english.Id, lang);

        // Ukweli's promise is that it quotes the document. Every excerpt comes
        // back exactly as the source published it, whatever language the
        // explanation around it is written in.
        Assert.Equal(
            english.Sources.Select(s => s.Excerpt),
            translated.Sources.Select(s => s.Excerpt));
    }

    [Fact]
    public async Task SwitchingLanguageTwiceReturnsTheSameWords()
    {
        using var client = _factory.CreateClient();

        var english = await AnalyzeAsync(client, lang: null);

        var first = await GetResultAsync(client, english.Id, "pcm");
        var second = await GetResultAsync(client, english.Id, "pcm");

        // Translations are stored on first use. A shared link has to read the
        // same for everyone who opens it, not be redrafted per visit.
        Assert.Equal(first.Explanation, second.Explanation);
        Assert.Equal(first.Action, second.Action);
    }

    [Fact]
    public async Task ATranslationIsStoredOnTheRowSoItIsNotDraftedTwice()
    {
        using var client = _factory.CreateClient();

        var english = await AnalyzeAsync(client, lang: null);
        await GetResultAsync(client, english.Id, "fr");

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UkweliDbContext>();
        var row = await db.ClaimAnalyses.AsNoTracking().FirstAsync(a => a.Id == english.Id);

        Assert.True(row.Translations.ContainsKey("fr"));
        Assert.Equal(row.Unknowns.Count, row.Translations["fr"].Unknowns.Count);
    }

    [Fact]
    public async Task AnUnknownLanguageFallsBackToEnglishRatherThanFailing()
    {
        using var client = _factory.CreateClient();

        var english = await AnalyzeAsync(client, lang: null);
        var asked = await GetResultAsync(client, english.Id, "xx");

        Assert.Equal(Language.English, asked.Language);
        Assert.Equal(english.Explanation, asked.Explanation);
    }

    [Fact]
    public async Task EveryLanguageIsOfferedSoAClientNeedNotGuess()
    {
        using var client = _factory.CreateClient();

        var english = await AnalyzeAsync(client, lang: null);

        Assert.Equal(
            [Language.English, Language.NigerianPidgin, Language.French],
            english.AvailableLanguages);
    }

    [Fact]
    public async Task ShareTextStillCarriesNoneOfTheUsersWords()
    {
        using var client = _factory.CreateClient();

        var english = await AnalyzeAsync(client, lang: null);
        var pidgin = await GetResultAsync(client, english.Id, "pcm");

        // The rule does not relax because the language changed: a forwarded
        // summary that quotes the rumour spreads the rumour.
        Assert.DoesNotContain("253 people", pidgin.ShareText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/r/" + english.Id, pidgin.ShareText, StringComparison.Ordinal);
        Assert.NotEqual(english.ShareText, pidgin.ShareText);
    }

    [Fact]
    public async Task AnalyzeAcceptsTheLanguageDirectly()
    {
        using var client = _factory.CreateClient();

        // A reader who already prefers Pidgin should not have to check in
        // English first and then switch.
        var pidgin = await AnalyzeAsync(client, lang: "pcm");

        Assert.Equal(Language.NigerianPidgin, pidgin.Language);
        Assert.Equal(VerdictStatus.Supported, pidgin.Status);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<AnalysisResponse> AnalyzeAsync(HttpClient client, string? lang)
    {
        var url = lang is null ? "/api/analyze" : $"/api/analyze?lang={lang}";
        var response = await client.PostAsJsonAsync(url, new AnalyzeRequest(null, SupportedSeed));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AnalysisResponse>();
        Assert.NotNull(body);
        return body;
    }

    private static async Task<AnalysisResponse> GetResultAsync(
        HttpClient client, string id, string lang)
    {
        var response = await client.GetAsync(new Uri($"/api/results/{id}?lang={lang}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AnalysisResponse>();
        Assert.NotNull(body);
        return body;
    }

    private static async Task ClearSeededAsync(UkweliDbContext db)
    {
        var seeded = await db.ClaimAnalyses.Where(a => a.IsSeeded).ToListAsync();
        db.ClaimAnalyses.RemoveRange(seeded);
        await db.SaveChangesAsync();
    }

    private static async Task SeedSourcesAsync(UkweliDbContext db)
    {
        var required = new[]
        {
            ("ncdc-lassa-sitrep-w34-2026-burden",
                "Cumulatively, 253 deaths have been reported with a Case Fatality Rate (CFR) of 24.0%."),
            ("ncdc-lassa-sitrep-w33-2026",
                "In week 33, the number of new confirmed cases increased from 4 reported in epi week 32 of 2026 to 14."),
        };

        foreach (var (id, excerpt) in required)
        {
            if (await db.Sources.AnyAsync(source => source.Id == id))
            {
                continue;
            }

            db.Sources.Add(new Source
            {
                Id = id,
                Issuer = "Nigeria Centre for Disease Control and Prevention",
                Title = "Lassa Fever Situation Report",
                SourceType = SourceType.PrimaryOfficial,
                Jurisdiction = Jurisdiction.Ng,
                Topic = Topic.DiseaseOutbreaks,
                PublishedAt = new DateOnly(2026, 8, 22),
                CheckedAt = new DateOnly(2026, 9, 19),
                Url = "https://ncdc.gov.ng/themes/common/files/sitreps/b0fedda076a0b27d21d5a09678dd69a0.pdf",
                CollectionUrl = "https://ncdc.gov.ng/diseases/sitreps",
                Excerpt = excerpt,
                Placeholder = false,
                Active = true,
            });
        }

        await db.SaveChangesAsync();
    }
}
