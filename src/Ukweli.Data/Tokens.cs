using System.Security.Cryptography;

namespace Ukweli.Data;

/// <summary>
/// Generates and hashes the opaque tokens used for sign-in links and sessions.
/// </summary>
/// <remarks>
/// Tokens are 32 random bytes, URL-safe base64. Only their SHA-256 hash is
/// persisted: a database backup then contains nothing that can be used to sign
/// in as anyone. Plain SHA-256 is right here — unlike a password, a token has
/// full entropy, so there is nothing to brute-force and no need for a slow KDF.
/// </remarks>
public static class Tokens
{
    public const int Bytes = 32;

    public static string New()
    {
        Span<byte> buffer = stackalloc byte[Bytes];
        RandomNumberGenerator.Fill(buffer);
        return Base64Url(buffer);
    }

    public static string Hash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var digest = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexStringLower(digest);
    }

    private static string Base64Url(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
