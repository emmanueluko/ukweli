using Ukweli.Contracts;

namespace Ukweli.Evidence.Ingestion;

/// <summary>
/// Where an automated ingest is allowed to look, and nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// This is the outer boundary on automation. A crawler that could follow any
/// link would eventually cite a blog that quotes a government notice, and a
/// verdict resting on that is a verdict resting on hearsay. Every candidate
/// document must come from a host named here.
/// </para>
/// <para>
/// Each entry also fixes the issuer and whether the publisher is primary or
/// secondary, so neither is inferred from a page that could claim anything
/// about itself.
/// </para>
/// </remarks>
public static class SourceRegistry
{
    /// <param name="Host">The exact host a document must be served from.</param>
    /// <param name="Issuer">The body, as it names itself.</param>
    /// <param name="SourceType">Primary official, or trusted secondary.</param>
    /// <param name="Jurisdiction">Where its documents apply.</param>
    /// <param name="Topics">The topics this publisher speaks to.</param>
    /// <param name="Collections">Index pages to look for new documents on.</param>
    public sealed record Publisher(
        string Host,
        string Issuer,
        SourceType SourceType,
        Jurisdiction Jurisdiction,
        IReadOnlyList<Topic> Topics,
        IReadOnlyList<string> Collections);

    /// <summary>
    /// The official bodies. Only these can carry a verdict: safety rule 3 needs
    /// a primary source to support or contradict anything.
    /// </summary>
    /// <remarks>
    /// Every entry here was checked: the host resolves, the collection returns
    /// 200, and the page contains links a crawler can follow. That is not a
    /// formality. The first version of this list was written from memory, and
    /// several of its hostnames and paths did not exist — plausible-looking
    /// addresses invented by the same habit this whole product exists to
    /// resist. See <see cref="Unverified"/> for what was removed and why.
    /// </remarks>
    public static readonly IReadOnlyList<Publisher> Official =
    [
        new("ncdc.gov.ng", "Nigeria Centre for Disease Control and Prevention",
            SourceType.PrimaryOfficial, Jurisdiction.Ng,
            [Topic.DiseaseOutbreaks],
            ["https://ncdc.gov.ng/diseases/sitreps"]),

        new("immigration.gov.ng", "Nigeria Immigration Service",
            SourceType.PrimaryOfficial, Jurisdiction.Ng,
            [Topic.IdentityDocuments],
            ["https://immigration.gov.ng/news/"]),

        new("inecnigeria.org", "Independent National Electoral Commission",
            SourceType.PrimaryOfficial, Jurisdiction.Ng,
            [Topic.Elections],
            // The root, not /news/: that path returns 404.
            ["https://inecnigeria.org/"]),
    ];

    /// <summary>
    /// Publishers that belong in this list but could not be verified, kept as a
    /// record so nobody re-adds them from memory.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Unreachable from our network</b> — connections time out, most likely
    /// geographic filtering, since several Nigerian government sites refuse
    /// traffic from outside the country: nphcda.gov.ng, firs.gov.ng,
    /// nimc.gov.ng, frsc.gov.ng, nema.gov.ng. Worth retrying from a Nigerian
    /// host; the application server is in Ireland.
    /// </para>
    /// <para>
    /// <b>Reachable, but the collection path is wrong</b> — the host answers
    /// while the listing does not: nafdac.gov.ng, cbn.gov.ng, lirs.gov.ng. Each
    /// needs somebody to open the site and record the real path.
    /// </para>
    /// <para>
    /// <b>Renders its listing in JavaScript</b> — lagosstate.gov.ng returns a
    /// two-kilobyte shell with no links in it. Crawling it needs a headless
    /// browser, which is a decision about what to run on the server, not a
    /// missing URL.
    /// </para>
    /// <para>
    /// <b>Requires payment or an account</b> — reuters.com answers 401. Ukweli
    /// should not cite what its readers cannot open.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyList<string> Unverified =
    [
        "nphcda.gov.ng", "firs.gov.ng", "nimc.gov.ng", "frsc.gov.ng", "nema.gov.ng",
        "nafdac.gov.ng", "cbn.gov.ng", "lirs.gov.ng", "lagosstate.gov.ng", "reuters.com",
    ];

    /// <summary>
    /// Reputable reporting, admitted as secondary.
    /// </summary>
    /// <remarks>
    /// These broaden what Ukweli can say something about, and safety rule 5
    /// already bounds what they can say: a secondary source is always marked
    /// context, never supports or conflicts, so it can add background or force
    /// mixed_unclear but can never on its own establish or contradict a claim.
    /// A claim that only news speaks to still returns insufficient_evidence.
    /// </remarks>
    public static readonly IReadOnlyList<Publisher> Reporting =
    [
        new("bbc.com", "BBC News", SourceType.SecondaryTrusted, Jurisdiction.Ng,
            [Topic.DiseaseOutbreaks, Topic.Emergencies, Topic.Elections],
            ["https://www.bbc.com/news/world/africa"]),

        new("premiumtimesng.com", "Premium Times", SourceType.SecondaryTrusted, Jurisdiction.Ng,
            [Topic.PaymentsLevies, Topic.HealthProducts, Topic.IdentityDocuments, Topic.Elections],
            ["https://www.premiumtimesng.com/news"]),

        new("dailytrust.com", "Daily Trust", SourceType.SecondaryTrusted, Jurisdiction.Ng,
            [Topic.PaymentsLevies, Topic.Emergencies, Topic.Elections],
            ["https://dailytrust.com/news/"]),
    ];

    public static IReadOnlyList<Publisher> All => [.. Official, .. Reporting];

    /// <summary>The publisher a URL belongs to, or null when it is off the allowlist.</summary>
    public static Publisher? Match(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host["www.".Length..]
            : uri.Host;

        return All.FirstOrDefault(publisher =>
            host.Equals(publisher.Host, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + publisher.Host, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsAllowed(string? url) => Match(url) is not null;
}
