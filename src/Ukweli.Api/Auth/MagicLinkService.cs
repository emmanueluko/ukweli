using Microsoft.EntityFrameworkCore;
using Ukweli.Data;
using Ukweli.Data.Entities;

namespace Ukweli.Api.Auth;

/// <summary>
/// Passwordless sign-in: request a link, click it, get a session.
/// </summary>
/// <remarks>
/// There are no password routes anywhere in Ukweli, so there is no password to
/// reuse, leak or reset.
/// </remarks>
public sealed class MagicLinkService(
    UkweliDbContext db,
    IMailer mailer,
    UkweliOptions options,
    ILogger<MagicLinkService> logger)
{
    /// <summary>Short: the link is delivered immediately and used immediately.</summary>
    public static readonly TimeSpan ValidFor = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How long a session survives without being used. Extended on use, so
    /// somebody who keeps checking claims stays signed in rather than being
    /// turned out abruptly a month after signing in.
    /// </summary>
    public static readonly TimeSpan SessionIdleLifetime = TimeSpan.FromDays(30);

    /// <summary>
    /// The hard limit measured from sign-in, whatever the activity.
    /// </summary>
    /// <remarks>
    /// Sliding expiry on its own would mean a stolen cookie stays valid forever
    /// as long as somebody keeps using it. This cap guarantees every session
    /// ends, and asking for a fresh link once a quarter is a small price.
    /// </remarks>
    public static readonly TimeSpan SessionAbsoluteLifetime = TimeSpan.FromDays(90);

    /// <summary>
    /// A session is only extended once it is this far into its window, so an
    /// active session does not write to the database on every request.
    /// </summary>
    private static readonly TimeSpan ExtendAfter = TimeSpan.FromDays(1);

    public const string CookieName = "ukweli_session";

    /// <summary>
    /// Issues a sign-in link and emails it.
    /// </summary>
    /// <remarks>
    /// This reports nothing about whether the address is already known. Callers
    /// answer 202 either way, so the endpoint cannot be used to discover who
    /// has an account.
    /// </remarks>
    public async Task RequestAsync(string email, CancellationToken cancellationToken)
    {
        var normalised = NormaliseEmail(email);
        var token = Tokens.New();

        db.MagicLinkTokens.Add(new MagicLinkToken
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = normalised,
            TokenHash = Tokens.Hash(token),
            ExpiresAt = DateTimeOffset.UtcNow.Add(ValidFor),
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await db.SaveChangesAsync(cancellationToken);

        var link = $"{options.AppUrl.TrimEnd('/')}/api/auth/verify?token={Uri.EscapeDataString(token)}";

        try
        {
            await mailer.SendMagicLinkAsync(normalised, link, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A delivery failure must not change what the caller sees. The
            // endpoint answers 202 whether or not the address is known, and if
            // an unreachable mail server turned that into a 500 the difference
            // would be visible from outside — which is the signal the fixed 202
            // exists to remove. It is logged instead, without the address.
            logger.MagicLinkFailed(0);
        }
    }

    /// <summary>
    /// Consumes a link and starts a session.
    /// </summary>
    /// <returns>The session cookie value, or null if the token is unusable.</returns>
    public async Task<string?> VerifyAsync(string? token, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var hash = Tokens.Hash(token.Trim());
        var now = DateTimeOffset.UtcNow;

        // Matched on the hash, so an expired or already-used token is found and
        // rejected rather than silently missing.
        var record = await db.MagicLinkTokens.FirstOrDefaultAsync(
            t => t.TokenHash == hash, cancellationToken);

        if (record is null || record.ConsumedAt is not null || record.ExpiresAt <= now)
        {
            logger.MagicLinkRejected(record is null ? "unknown" : "expired or already used");
            return null;
        }

        record.ConsumedAt = now;

        var user = await db.AuthUsers.FirstOrDefaultAsync(
            u => u.Email == record.Email, cancellationToken);

        if (user is null)
        {
            user = new AuthUser
            {
                Id = Guid.NewGuid().ToString("N"),
                Email = record.Email,
                CreatedAt = now,
            };
            db.AuthUsers.Add(user);
        }

        user.LastSignInAt = now;

        var sessionToken = Tokens.New();
        db.AuthSessions.Add(new AuthSession
        {
            Id = Tokens.Hash(sessionToken),
            UserId = user.Id,
            ExpiresAt = now.Add(SessionIdleLifetime),
            CreatedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);
        return sessionToken;
    }

    /// <summary>
    /// Resolves a cookie to a live session, extending its window if it has been
    /// in use.
    /// </summary>
    /// <returns>
    /// The user id, and the new expiry when the window moved so the caller can
    /// re-issue the cookie. Null when the cookie names no live session.
    /// </returns>
    public async Task<ResolvedSession?> ResolveAsync(
        string? sessionToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        var hash = Tokens.Hash(sessionToken.Trim());
        var now = DateTimeOffset.UtcNow;

        var session = await db.AuthSessions.FirstOrDefaultAsync(s => s.Id == hash, cancellationToken);

        if (session is null || session.ExpiresAt <= now)
        {
            return null;
        }

        // Past the hard limit the session is over, however recently it was used.
        var absoluteEnd = session.CreatedAt.Add(SessionAbsoluteLifetime);
        if (absoluteEnd <= now)
        {
            db.AuthSessions.Remove(session);
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        var proposed = now.Add(SessionIdleLifetime);
        if (proposed > absoluteEnd)
        {
            proposed = absoluteEnd;
        }

        if (proposed - session.ExpiresAt >= ExtendAfter)
        {
            session.ExpiresAt = proposed;
            await db.SaveChangesAsync(cancellationToken);
            return new ResolvedSession(session.UserId, proposed);
        }

        return new ResolvedSession(session.UserId, null);
    }

    /// <param name="UserId">Who the session belongs to.</param>
    /// <param name="RenewedUntil">
    /// Set when the window moved and the cookie should be re-issued; null when
    /// it was recent enough to leave alone.
    /// </param>
    public sealed record ResolvedSession(string UserId, DateTimeOffset? RenewedUntil);

    public async Task SignOutAsync(string? sessionToken, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return;
        }

        var hash = Tokens.Hash(sessionToken.Trim());
        var session = await db.AuthSessions.FirstOrDefaultAsync(s => s.Id == hash, cancellationToken);

        if (session is not null)
        {
            // Deleted, not marked inactive: a signed-out session should stop
            // existing rather than linger as a row that might be honoured again.
            db.AuthSessions.Remove(session);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<AuthUser?> FindUserAsync(string userId, CancellationToken cancellationToken) =>
        await db.AuthUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

    internal static string NormaliseEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        return email.Trim().ToLowerInvariant();
    }

    /// <summary>A shallow check: the only real test of an address is whether mail arrives.</summary>
    internal static bool LooksLikeEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return false;
        }

        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@', StringComparison.Ordinal);

        return at > 0
            && at < trimmed.Length - 1
            && trimmed.IndexOf('@', at + 1) < 0
            && trimmed.LastIndexOf('.') > at + 1
            && !trimmed.Contains(' ', StringComparison.Ordinal);
    }
}
