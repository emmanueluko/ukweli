namespace Ukweli.Data.Entities;

/// <summary>
/// A person who has signed in. Email and nothing else.
/// </summary>
/// <remarks>
/// There is deliberately no name, no password hash and no profile. Auth exists
/// to gate saved history and for no other purpose, so the less it holds the
/// less there is to leak.
/// </remarks>
public class AuthUser
{
    public string Id { get; set; } = string.Empty;

    /// <summary>Stored lower-cased, and unique.</summary>
    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastSignInAt { get; set; }
}

/// <summary>
/// A single-use sign-in link.
/// </summary>
/// <remarks>
/// Only the SHA-256 hash of the token is stored. A stolen database backup then
/// yields no working sign-in links, which is the whole reason the raw value
/// exists only in the email.
/// </remarks>
public class MagicLinkToken
{
    public string Id { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>SHA-256 of the token, hex encoded. The raw token is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>Set the moment the link is used, so it cannot be replayed.</summary>
    public DateTimeOffset? ConsumedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}

/// <summary>A signed-in session, keyed by the hash of its cookie value.</summary>
public class AuthSession
{
    /// <summary>SHA-256 of the cookie value, hex encoded.</summary>
    public string Id { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
