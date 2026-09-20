using System.Net;
using System.Net.Http.Json;
using Ukweli.Api.Auth;
using Ukweli.Contracts;

namespace Ukweli.Api.Tests;

/// <summary>
/// The demo must work fully with auth off, and auth must gate saved history
/// only. These run with <c>AUTH_ENABLED=false</c>, the shipped default.
/// </summary>
[Collection("database")]
public class AuthDisabledTests : IDisposable
{
    private readonly UkweliApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/api/examples")]
    public async Task PublicRoutesStillWork(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/me/analyses")]
    [InlineData("/api/auth/verify?token=anything")]
    public async Task EveryAuthRouteIsHiddenEntirely(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);

        // Not 401 — the routes are not mapped at all.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RequestingASignInLinkIsAlsoHidden()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/magic-link", new MagicLinkRequest("someone@example.com"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AnalyzeStillWorksAnonymously()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyze", new AnalyzeRequest(null, "seed-unlisted-local-fee"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

/// <summary>Auth on: the routes exist, and they protect only saved history.</summary>
[Collection("database")]
public class AuthEnabledTests : IDisposable
{
    private readonly UkweliApiFactory _factory = new(authEnabled: true);

    public void Dispose()
    {
        _factory.Dispose();
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData("/api/me")]
    [InlineData("/api/me/analyses")]
    public async Task SavedHistoryRequiresASession(string path)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(path);
        var body = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal(ErrorCodes.Unauthorized, body.Error.Code);
    }

    [Theory]
    [InlineData("/healthz")]
    [InlineData("/api/examples")]
    public async Task PublicRoutesStayPublic(string path)
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task AnalyzeStaysPublic()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/analyze", new AnalyzeRequest(null, "seed-unlisted-local-fee"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AcceptsASignInRequestWithoutRevealingWhetherTheAddressIsKnown()
    {
        using var client = _factory.CreateClient();

        var first = await client.PostAsJsonAsync(
            "/api/auth/magic-link", new MagicLinkRequest("someone@example.com"));
        var second = await client.PostAsJsonAsync(
            "/api/auth/magic-link", new MagicLinkRequest("nobody-at-all@example.com"));

        // Identical answers, so the endpoint cannot be used to enumerate accounts.
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);
        Assert.Equal(
            await first.Content.ReadAsStringAsync(),
            await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StillAccepts202WhenTheMailServerIsUnreachable()
    {
        // Regression: CI has no Mailpit, and an escaping socket error turned the
        // fixed 202 into a 500. That difference is visible from outside, which
        // is exactly the signal the fixed 202 exists to remove — a dead mail
        // server is an operational problem, not something a stranger should be
        // able to probe for. Port 1 has nothing listening.
        using var offline = new UkweliApiFactory(authEnabled: true, smtpPort: 1);
        using var client = offline.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/magic-link", new MagicLinkRequest("someone@example.com"));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("two@@example.com")]
    [InlineData("has space@example.com")]
    public async Task RejectsAnAddressThatCannotReceiveMail(string? email)
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/magic-link", new MagicLinkRequest(email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a-token-that-was-never-issued")]
    public async Task SendsAnUnusableLinkBackToTheSignInScreen(string token)
    {
        // The person following this came from an email client. They get a
        // screen that explains, not a JSON error envelope.
        using var client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

        var response = await client.GetAsync($"/api/auth/verify?token={token}");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Contains("/sign-in?error=link_expired", response.Headers.Location!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NeverSetsASessionCookieForABadLink()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/verify?token=nonsense");

        Assert.False(response.Headers.TryGetValues("Set-Cookie", out _));
    }
}
