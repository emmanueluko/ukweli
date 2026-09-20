using System.Text.Json.Serialization;

namespace Ukweli.Contracts;

/// <summary>
/// The languages Ukweli answers in.
/// </summary>
/// <remarks>
/// <para>
/// Chosen for who actually receives forwarded civic rumours in West Africa:
/// English is the official language of Nigeria, Nigerian Pidgin is what tens of
/// millions of people actually speak day to day, and French covers most of the
/// countries next door.
/// </para>
/// <para>
/// One thing is never translated: a source excerpt. The promise is that Ukweli
/// quotes the document, and a translated quotation is no longer a quotation.
/// The explanation is rendered around the excerpt; the excerpt itself stays in
/// the language the authority published it in.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<Language>))]
public enum Language
{
    [JsonStringEnumMemberName("en")]
    English,

    /// <summary>Nigerian Pidgin. ISO 639-3 <c>pcm</c>.</summary>
    [JsonStringEnumMemberName("pcm")]
    NigerianPidgin,

    [JsonStringEnumMemberName("fr")]
    French,
}

/// <summary>An analysis rendered in one language.</summary>
/// <param name="Explanation">What the evidence says, in full.</param>
/// <param name="SimpleExplanation">The same evidence, shorter.</param>
/// <param name="Unknowns">What this check could not establish.</param>
/// <param name="Action">The one safe next step.</param>
public sealed record Translation(
    string Explanation,
    string SimpleExplanation,
    IReadOnlyList<string> Unknowns,
    string Action);

public static class Languages
{
    /// <summary>Every language Ukweli answers in, English first.</summary>
    public static IReadOnlyList<Language> All { get; } =
        [Language.English, Language.NigerianPidgin, Language.French];

    /// <summary>The language a request asked for, defaulting to English.</summary>
    public static Language Parse(string? value) =>
        WireNames.TryParse<Language>(value, out var language) ? language : Language.English;

    /// <summary>How the language is named in its own language, for a switcher.</summary>
    public static string EndonymOf(Language language) => language switch
    {
        Language.English => "English",
        Language.NigerianPidgin => "Pidgin",
        Language.French => "Français",
        _ => "English",
    };
}
