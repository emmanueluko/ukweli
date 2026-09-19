using System.Text.Json.Serialization;

namespace Ukweli.Contracts;

/// <summary>
/// The four verdicts Ukweli can return. There is deliberately no numeric
/// confidence anywhere in the system (safety rule 6) — a claim resolves to one
/// of these words, or it resolves to <see cref="InsufficientEvidence"/>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<VerdictStatus>))]
public enum VerdictStatus
{
    [JsonStringEnumMemberName("supported")]
    Supported,

    [JsonStringEnumMemberName("contradicted")]
    Contradicted,

    [JsonStringEnumMemberName("mixed_unclear")]
    MixedUnclear,

    [JsonStringEnumMemberName("insufficient_evidence")]
    InsufficientEvidence,
}

/// <summary>
/// Whether a source is an official primary document or a trusted secondary
/// report. The distinction carries weight: safety rules 3 and 5 mean a
/// secondary source can never on its own support, contradict, or override.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceType>))]
public enum SourceType
{
    [JsonStringEnumMemberName("primary_official")]
    PrimaryOfficial,

    [JsonStringEnumMemberName("secondary_trusted")]
    SecondaryTrusted,
}

/// <summary>Jurisdiction a source or claim applies to: Nigeria, or Lagos State.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Jurisdiction>))]
public enum Jurisdiction
{
    [JsonStringEnumMemberName("NG")]
    Ng,

    [JsonStringEnumMemberName("NG-LA")]
    NgLa,
}

/// <summary>The civic areas the curated corpus covers.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<Topic>))]
public enum Topic
{
    [JsonStringEnumMemberName("payments_levies")]
    PaymentsLevies,

    [JsonStringEnumMemberName("disease_outbreaks")]
    DiseaseOutbreaks,
}

/// <summary>How a cited source stands in relation to the claim being checked.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SourceRelation>))]
public enum SourceRelation
{
    [JsonStringEnumMemberName("supports")]
    Supports,

    [JsonStringEnumMemberName("conflicts")]
    Conflicts,

    [JsonStringEnumMemberName("context")]
    Context,
}
