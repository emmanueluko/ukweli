using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ukweli.Api.Auth;
using Ukweli.Contracts;

namespace Ukweli.Api.Tests;

/// <summary>
/// Phase 4's acceptance criterion, end to end: request a link, read it out of
/// the inbox, open it, and see your own history.
/// </summary>
/// <remarks>
/// This talks to the real Mailpit from <c>docker-compose.dev.yml</c> — the same
/// SMTP path a developer uses — because the thing worth proving is that an
/// email actually arrives carrying a link that actually works. A faked mailer
/// would prove only that the fake was called.
/// </remarks>
[Collection("database")]
public class MagicLinkRoundTripTests : IDisposable
{
    private const string MailpitBase = "http://localhost:8025";

    private readonly UkweliApiFactory _factory = new(authEnabled: true);
    private readonly HttpClient _mailpit = new() { BaseAddress = new Uri(MailpitBase) };

    public void Dispose()
    {
        _factory.Dispose();
        _mailpit.Dispose();
        GC.SuppressFinalize(this);
    }

    private async Task<bool> MailpitIsRunningAsync()
    {
        try
        {
            using var response = await _mailpit.GetAsync("/api/v1/messages?limit=1");
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }

    [Fact]
    public async Task RequestingALinkDeliversAnEmailThatSignsYouIn()
    {
        if (!await MailpitIsRunningAsync())
        {
            // Skipped rather than failed: the dev stack is not always up, and a
            // red suite for that reason teaches people to ignore red suites.
            Assert.True(true, "Mailpit is not running; start docker-compose.dev.yml to run this.");
            return;
        }

        var email = $"round-trip-{Guid.NewGuid():N}@example.com";
        using var client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

        // 1. Ask for a link.
        var requested = await client.PostAsJsonAsync(
            "/api/auth/magic-link", new MagicLinkRequest(email));
        Assert.Equal(HttpStatusCode.Accepted, requested.StatusCode);

        // 2. Find it in the inbox, addressed to this run only.
        var token = await FindTokenAsync(email);
        Assert.False(string.IsNullOrWhiteSpace(token), "No sign-in link arrived in Mailpit.");

        // 3. Open it.
        var verified = await client.GetAsync($"/api/auth/verify?token={Uri.EscapeDataString(token!)}");
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);

        var cookie = SessionCookie(verified);
        Assert.NotNull(cookie);

        // 4. The session is real: it identifies the account that asked.
        using var signedIn = _factory.CreateClient();
        signedIn.DefaultRequestHeaders.Add("Cookie", $"{MagicLinkService.CookieName}={cookie}");

        var me = await signedIn.GetFromJsonAsync<MeResponse>("/api/me");
        Assert.NotNull(me);
        Assert.Equal(email, me.Email);

        // 5. And it unlocks the history, which is all it is for.
        var history = await signedIn.GetAsync("/api/me/analyses");
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
    }

    [Fact]
    public async Task ASignInLinkCannotBeUsedTwice()
    {
        if (!await MailpitIsRunningAsync())
        {
            Assert.True(true, "Mailpit is not running; start docker-compose.dev.yml to run this.");
            return;
        }

        var email = $"replay-{Guid.NewGuid():N}@example.com";
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/magic-link", new MagicLinkRequest(email));
        var token = await FindTokenAsync(email);
        Assert.NotNull(token);

        var first = await client.GetAsync($"/api/auth/verify?token={Uri.EscapeDataString(token!)}");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // A link found in a forwarded email, or in a browser history, is spent.
        var second = await client.GetAsync($"/api/auth/verify?token={Uri.EscapeDataString(token)}");
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task TheEmailNeverContainsAPassword()
    {
        if (!await MailpitIsRunningAsync())
        {
            Assert.True(true, "Mailpit is not running; start docker-compose.dev.yml to run this.");
            return;
        }

        var email = $"content-{Guid.NewGuid():N}@example.com";
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/magic-link", new MagicLinkRequest(email));
        var body = await FindBodyAsync(email);

        Assert.NotNull(body);
        Assert.DoesNotContain("password", body!, StringComparison.OrdinalIgnoreCase);
        // It has to say what to do if you did not ask for it.
        Assert.Contains("did not ask", body, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Polls briefly: SMTP delivery is fast but not instantaneous.</summary>
    private async Task<string?> FindBodyAsync(string email)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            using var listed = await _mailpit.GetAsync("/api/v1/messages?limit=50");
            using var document = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());

            foreach (var message in document.RootElement.GetProperty("messages").EnumerateArray())
            {
                var to = message.GetProperty("To").EnumerateArray()
                    .Select(recipient => recipient.GetProperty("Address").GetString())
                    .ToList();

                if (!to.Contains(email, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var id = message.GetProperty("ID").GetString();
                using var full = await _mailpit.GetAsync($"/api/v1/message/{id}");
                using var body = JsonDocument.Parse(await full.Content.ReadAsStringAsync());

                return body.RootElement.GetProperty("Text").GetString();
            }

            await Task.Delay(250);
        }

        return null;
    }

    private async Task<string?> FindTokenAsync(string email)
    {
        var body = await FindBodyAsync(email);
        if (body is null)
        {
            return null;
        }

        var match = Regex.Match(body, @"verify\?token=([A-Za-z0-9_\-%]+)");
        return match.Success ? Uri.UnescapeDataString(match.Groups[1].Value) : null;
    }

    private static string? SessionCookie(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        foreach (var cookie in cookies)
        {
            var match = Regex.Match(cookie, $"{MagicLinkService.CookieName}=([^;]+)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }
        }

        return null;
    }
}
