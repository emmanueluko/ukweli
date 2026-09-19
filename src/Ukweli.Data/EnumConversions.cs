using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Ukweli.Contracts;

namespace Ukweli.Data;

/// <summary>
/// EF Core value converters that persist an enum using the same spelling the
/// API and the curated JSON files use, via <see cref="WireNames"/>.
/// </summary>
internal static class EnumConversions
{
    public static ValueConverter<TEnum, string> For<TEnum>() where TEnum : struct, Enum =>
        new(value => WireNames.ToWire(value), wire => WireNames.Parse<TEnum>(wire));

    /// <summary>
    /// The same conversion for a nullable column. Null round-trips as null:
    /// a claim that names no place has no jurisdiction, and there is no
    /// spelling for "unknown" among the valid ones.
    /// </summary>
    public static ValueConverter<TEnum?, string?> NullableFor<TEnum>() where TEnum : struct, Enum =>
        new(value => value.HasValue ? WireNames.ToWire(value.Value) : null,
            wire => wire == null ? null : WireNames.Parse<TEnum>(wire));
}
