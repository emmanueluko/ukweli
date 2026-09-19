using System.Security.Cryptography;

namespace Ukweli.Data;

/// <summary>
/// Generates the short ids that identify an analysis and appear in its
/// <c>/r/{id}</c> share link.
/// </summary>
/// <remarks>
/// nanoid(10) over a URL-safe alphabet, as the specification asks. Ten
/// characters of this alphabet is about 59 bits — not secret, but far too
/// sparse to walk, which matters because a result link is shareable and an
/// enumerable one would expose what other people checked.
/// </remarks>
public static class AnalysisId
{
    public const int Length = 10;

    private const string Alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz-_";

    public static string New()
    {
        Span<byte> bytes = stackalloc byte[Length];
        RandomNumberGenerator.Fill(bytes);

        Span<char> characters = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            // The alphabet is 64 characters, so masking the low six bits is a
            // uniform choice with no modulo bias.
            characters[i] = Alphabet[bytes[i] & 63];
        }

        return new string(characters);
    }

    /// <summary>Whether a string could be an id, used to reject nonsense before a query.</summary>
    public static bool IsWellFormed(string? value) =>
        value is { Length: Length } && value.All(Alphabet.Contains);
}
