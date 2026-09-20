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

/// <summary>
/// The civic areas the curated corpus covers.
/// </summary>
/// <remarks>
/// Each value is a kind of claim that circulates and costs people money, health
/// or time when believed. Adding one means committing to curate sources for it:
/// a topic with no sources answers every claim with insufficient_evidence.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<Topic>))]
public enum Topic
{
    /// <summary>Levies, taxes, fees and payment demands.</summary>
    [JsonStringEnumMemberName("payments_levies")]
    PaymentsLevies,

    /// <summary>Outbreaks and case numbers.</summary>
    [JsonStringEnumMemberName("disease_outbreaks")]
    DiseaseOutbreaks,

    /// <summary>Medicines, recalls, counterfeits and claimed cures.</summary>
    [JsonStringEnumMemberName("health_products")]
    HealthProducts,

    /// <summary>National ID, passports, licences and their deadlines.</summary>
    [JsonStringEnumMemberName("identity_documents")]
    IdentityDocuments,

    /// <summary>Floods, disasters and emergency instructions.</summary>
    [JsonStringEnumMemberName("emergencies")]
    Emergencies,

    /// <summary>Registration, dates and electoral process.</summary>
    [JsonStringEnumMemberName("elections")]
    Elections,
}

/// <summary>
/// Who put a record in the curated store.
/// </summary>
/// <remarks>
/// Recorded because the two are not equally trustworthy. A human read the
/// document and chose the passage; an automated ingest proved only that the
/// excerpt appears verbatim in what was fetched, which cannot tell whether a
/// true sentence misleads in context. Keeping the distinction visible means an
/// audit can ask "which of these did nobody read?".
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<Curation>))]
public enum Curation
{
    /// <summary>A person read the document and chose the excerpt.</summary>
    [JsonStringEnumMemberName("human")]
    Human,

    /// <summary>Ingested by machine, excerpt verified verbatim against the fetched document.</summary>
    [JsonStringEnumMemberName("automated")]
    Automated,
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
