using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json.Serialization;

namespace Ukweli.Contracts;

/// <summary>
/// The single source of truth for how an enum is spelled outside the process —
/// <c>primary_official</c>, <c>NG-LA</c>, <c>insufficient_evidence</c>.
/// </summary>
/// <remarks>
/// JSON responses get these spellings from
/// <see cref="JsonStringEnumMemberNameAttribute"/>. The database stores the same
/// strings, read through this helper rather than through EF Core's default
/// <c>HasConversion&lt;string&gt;()</c>, which would write the C# member name
/// (<c>PrimaryOfficial</c>) instead and quietly disagree with every API
/// response and every curated JSON file.
/// </remarks>
public static class WireNames
{
    private static readonly ConcurrentDictionary<Type, WireNameMap> Maps = new();

    /// <summary>The wire spelling of <paramref name="value"/>.</summary>
    public static string ToWire<TEnum>(TEnum value) where TEnum : struct, Enum =>
        MapFor(typeof(TEnum)).ToWire.TryGetValue(value, out var wire)
            ? wire
            : throw new ArgumentOutOfRangeException(
                nameof(value), value, $"{typeof(TEnum).Name} has no member with this value.");

    /// <summary>Parses a wire spelling back to its enum member.</summary>
    /// <exception cref="FormatException">The value is not a known spelling.</exception>
    public static TEnum Parse<TEnum>(string? wire) where TEnum : struct, Enum =>
        TryParse<TEnum>(wire, out var value)
            ? value
            : throw new FormatException(
                $"'{wire}' is not a valid {typeof(TEnum).Name}. Expected one of: "
                + string.Join(", ", AllWireNames<TEnum>()) + ".");

    public static bool TryParse<TEnum>(string? wire, out TEnum value) where TEnum : struct, Enum
    {
        value = default;

        if (string.IsNullOrWhiteSpace(wire))
        {
            return false;
        }

        if (!MapFor(typeof(TEnum)).FromWire.TryGetValue(wire.Trim(), out var boxed))
        {
            return false;
        }

        value = (TEnum)boxed;
        return true;
    }

    /// <summary>Every valid spelling for <typeparamref name="TEnum"/>, in declaration order.</summary>
    public static IReadOnlyList<string> AllWireNames<TEnum>() where TEnum : struct, Enum =>
        MapFor(typeof(TEnum)).Ordered;

    private static WireNameMap MapFor(Type enumType) => Maps.GetOrAdd(enumType, Build);

    private static WireNameMap Build(Type enumType)
    {
        var toWire = new Dictionary<object, string>();
        // Case-insensitive so a hand-edited JSON file saying "ng-la" still loads.
        var fromWire = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        var ordered = new List<string>();

        foreach (var field in enumType.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var value = field.GetValue(null)!;
            var wire = field.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
                ?? field.Name;

            toWire[value] = wire;
            fromWire[wire] = value;
            ordered.Add(wire);
        }

        return new WireNameMap(toWire, fromWire, ordered);
    }

    private sealed record WireNameMap(
        Dictionary<object, string> ToWire,
        Dictionary<string, object> FromWire,
        IReadOnlyList<string> Ordered);
}
