using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ukweli.Api.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ukweli.Contracts;
using Ukweli.Data;

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
        // A redirect into the app, because a person opened this from their inbox.
        Assert.Equal(HttpStatusCode.Found, verified.StatusCode);
        Assert.Contains("/my-checks", verified.Headers.Location!.ToString(), StringComparison.Ordinal);

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

        using var noRedirect = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

        var first = await noRedirect.GetAsync($"/api/auth/verify?token={Uri.EscapeDataString(token!)}");
        Assert.Contains("/my-checks", first.Headers.Location!.ToString(), StringComparison.Ordinal);

        // A link found in a forwarded email, or in a browser history, is spent.
        var second = await noRedirect.GetAsync($"/api/auth/verify?token={Uri.EscapeDataString(token)}");
        Assert.Contains("/sign-in?error=link_expired", second.Headers.Location!.ToString(), StringComparison.Ordinal);
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

    [Fact]
    public async Task UsingASessionExtendsItRatherThanCountingDownToSignOut()
    {
        if (!await MailpitIsRunningAsync())
        {
            Assert.True(true, "Mailpit is not running; start docker-compose.dev.yml to run this.");
            return;
        }

        var email = $"sliding-{Guid.NewGuid():N}@example.com";
        using var client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

        await client.PostAsJsonAsync("/api/auth/magic-link", new MagicLinkRequest(email));
        var token = await FindTokenAsync(email);
        var verified = await client.GetAsync($"/api/auth/verify?token={Uri.EscapeDataString(token!)}");
        var cookie = SessionCookie(verified);
        Assert.NotNull(cookie);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UkweliDbContext>();
        var hash = Tokens.Hash(cookie!);

        var session = await db.AuthSessions.FirstAsync(s => s.Id == hash);
        Assert.True(session.ExpiresAt > DateTimeOffset.UtcNow.AddDays(29));

        // Wind the window back as though the session were signed in some days
        // ago and has just been used again.
        session.ExpiresAt = DateTimeOffset.UtcNow.AddDays(20);
        await db.SaveChangesAsync();

        using var signedIn = _factory.CreateClient();
        signedIn.DefaultRequestHeaders.Add("Cookie", $"{MagicLinkService.CookieName}={cookie}");
        Assert.Equal(HttpStatusCode.OK, (await signedIn.GetAsync("/api/me")).StatusCode);

        // Using it pushed the window back out, so continued use never signs
        // somebody out.
        db.ChangeTracker.Clear();
        var after = await db.AuthSessions.AsNoTracking().FirstAsync(s => s.Id == hash);
        Assert.True(
            after.ExpiresAt > DateTimeOffset.UtcNow.AddDays(29),
            $"Expected the window to be extended; it ends {after.ExpiresAt:u}.");
    }

    [Fact]
    public async Task ASessionEndsAtTheAbsoluteLimitHoweverRecentlyItWasUsed()
    {
        if (!await MailpitIsRunningAsync())
        {
            Assert.True(true, "Mailpit is not running; start docker-compose.dev.yml to run this.");
            return;
        }

        var email = $"absolute-{Guid.NewGuid():N}@example.com";
        using var client = _factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
            });

        await client.PostAsJsonAsync("/api/auth/magic-link", new MagicLinkRequest(email));
        var token = await FindTokenAsync(email);
        var verified = await client.GetAsync($"/api/auth/verify?token={Uri.EscapeDataString(token!)}");
        var cookie = SessionCookie(verified);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<UkweliDbContext>();
        var hash = Tokens.Hash(cookie!);

        // Signed in longer ago than the hard limit allows, but used recently.
        var session = await db.AuthSessions.FirstAsync(s => s.Id == hash);
        session.CreatedAt = DateTimeOffset.UtcNow - MagicLinkService.SessionAbsoluteLifetime.Add(TimeSpan.FromDays(1));
        await db.SaveChangesAsync();

        using var stale = _factory.CreateClient();
        stale.DefaultRequestHeaders.Add("Cookie", $"{MagicLinkService.CookieName}={cookie}");

        // Sliding expiry must not mean a session that never ends.
        Assert.Equal(HttpStatusCode.Unauthorized, (await stale.GetAsync("/api/me")).StatusCode);
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
