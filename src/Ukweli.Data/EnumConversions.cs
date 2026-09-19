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
}
