using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Ukweli.Contracts;

namespace Ukweli.Data;

/// <summary>
/// Stores collections as jsonb, the way the specification's data model
/// describes <c>unknowns</c> and <c>sourceIds</c>.
/// </summary>
/// <remarks>
/// Each converter is paired with a value comparer. Without one EF Core compares
/// collection properties by reference, so a list mutated in place would never
/// be detected as changed and the update would be silently dropped.
/// </remarks>
internal static class JsonConversions
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static ValueConverter<List<T>, string> ForList<T>() =>
        new(value => JsonSerializer.Serialize(value, Options),
            json => JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>());

    public static ValueComparer<List<T>> ListComparer<T>() =>
        new((left, right) => left != null && right != null && left.SequenceEqual(right),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item)),
            value => value.ToList());

    /// <summary>
    /// A map from source id to how that source stands to the claim. The enum is
    /// written using its wire spelling so the column reads the same way the API
    /// does.
    /// </summary>
    public static ValueConverter<Dictionary<string, TValue>, string> ForDictionary<TValue>()
        where TValue : struct, Enum =>
        new(value => Serialise(value),
            json => Deserialise<TValue>(json));

    public static ValueComparer<Dictionary<string, TValue>> DictionaryComparer<TValue>()
        where TValue : struct, Enum =>
        // The equality check is a method call rather than an inline lambda:
        // ValueComparer takes an expression tree, which cannot declare an
        // `out var` for TryGetValue.
        new((left, right) => AreEqual(left, right),
            value => value.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
            value => new Dictionary<string, TValue>(value));

    private static bool AreEqual<TValue>(
        Dictionary<string, TValue>? left, Dictionary<string, TValue>? right)
        where TValue : struct, Enum
    {
        if (left is null || right is null)
        {
            return left is null && right is null;
        }

        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var other) || !value.Equals(other))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Stores the per-language prose as jsonb.</summary>
    public static ValueConverter<Dictionary<string, Translation>, string> ForTranslations() =>
        new(value => JsonSerializer.Serialize(value, Options),
            json => JsonSerializer.Deserialize<Dictionary<string, Translation>>(json, Options)
                ?? new Dictionary<string, Translation>());

    public static ValueComparer<Dictionary<string, Translation>> TranslationComparer() =>
        // Compared by serialised value: a translation is written once and read
        // thereafter, so correctness matters more than comparison speed.
        new((left, right) => JsonSerializer.Serialize(left, Options) == JsonSerializer.Serialize(right, Options),
            value => JsonSerializer.Serialize(value, Options).GetHashCode(StringComparison.Ordinal),
            value => JsonSerializer.Deserialize<Dictionary<string, Translation>>(
                JsonSerializer.Serialize(value, Options), Options) ?? new Dictionary<string, Translation>());

    private static string Serialise<TValue>(Dictionary<string, TValue> value)
        where TValue : struct, Enum =>
        JsonSerializer.Serialize(
            value.ToDictionary(pair => pair.Key, pair => WireNames.ToWire(pair.Value)),
            Options);

    private static Dictionary<string, TValue> Deserialise<TValue>(string json)
        where TValue : struct, Enum
    {
        var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(json, Options);

        return raw is null
            ? []
            : raw.ToDictionary(pair => pair.Key, pair => WireNames.Parse<TValue>(pair.Value));
    }
}
