using System.Globalization;
using Ukweli.Contracts;

namespace Ukweli.Evidence;

/// <summary>
/// Chooses which curated excerpts a claim gets checked against.
/// </summary>
/// <remarks>
/// <para>
/// No AI, and deliberately simple: the corpus is capped at fifteen records, so
/// keyword overlap with a relevance floor is the right tool. Embeddings and a
/// vector store would add infrastructure, latency and opacity to a problem that
/// fits in a loop.
/// </para>
/// <para>
/// What matters is the floor, not the ranking. Returning a weakly related
/// source is worse than returning nothing: nothing yields
/// <c>insufficient_evidence</c>, which is honest, while a weak match invites a
/// verdict resting on a document that does not actually address the claim.
/// </para>
/// </remarks>
public static class Retriever
{
    /// <summary>A source must clear this to be shown to the model at all.</summary>
    public const double RelevanceFloor = 2.0;

    /// <summary>More than this and the prompt is a reading list, not evidence.</summary>
    public const int MaxResults = 5;

    private const double TopicMatchWeight = 2.0;
    private const double JurisdictionMatchWeight = 1.5;
    private const double KeywordWeight = 1.0;
    private const double TitleKeywordWeight = 0.5;

    /// <summary>
    /// Words carrying no topical signal. Without this, "the" and "a" would make
    /// every source look relevant to every claim.
    /// </summary>
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "if", "then", "than", "that", "this", "these",
        "those", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had",
        "do", "does", "did", "will", "would", "shall", "should", "can", "could", "may",
        "might", "must", "to", "of", "in", "on", "at", "by", "for", "with", "from", "as",
        "it", "its", "they", "them", "their", "we", "our", "you", "your", "i", "my", "me",
        "he", "she", "his", "her", "all", "any", "every", "some", "no", "not", "now",
        "new", "said", "says", "say", "also", "very", "just", "only", "more", "most",
        "there", "here", "who", "what", "when", "where", "which", "how", "why",
    };

    /// <summary>Terms that mark a claim as belonging to a topic.</summary>
    private static readonly Dictionary<Topic, string[]> TopicVocabulary = new()
    {
        [Topic.PaymentsLevies] =
        [
            "levy", "levies", "tax", "taxes", "fee", "fees", "charge", "charges", "payment",
            "pay", "paid", "paying", "rate", "rates", "duty", "bill", "invoice", "naira",
            "revenue", "permit", "licence", "license", "registration", "fine", "penalty",
            "collector", "sealed", "seal", "shop", "shops", "trader", "traders", "market",
            "household", "households", "waste", "refuse", "tenement", "land", "property",
            "remittance", "circular", "assessment", "deduction", "withholding", "stamp",
        ],
        [Topic.DiseaseOutbreaks] =
        [
            "disease", "outbreak", "epidemic", "infection", "infected", "virus", "fever",
            "lassa", "cholera", "diphtheria", "mpox", "measles", "meningitis", "polio",
            "covid", "symptom", "symptoms", "case", "cases", "death", "deaths", "vaccine",
            "vaccination", "cure", "cured", "treatment", "hospital", "clinic", "health",
            "quarantine", "isolation", "contagious", "spread", "herbal", "remedy",
            "surveillance", "sitrep", "confirmed", "suspected", "fatality",
        ],
        [Topic.HealthProducts] =
        [
            "drug", "drugs", "medicine", "medicines", "recall", "recalled", "counterfeit",
            "fake", "substandard", "falsified", "batch", "nafdac", "registration", "banned",
            "withdrawn", "alert", "product", "syrup", "tablet", "capsule", "injection",
            "contaminated", "adulterated", "supplement", "herbal", "unregistered", "expired",
        ],
        [Topic.IdentityDocuments] =
        [
            "identity", "identification", "nin", "passport", "licence", "license", "card",
            "enrolment", "enrollment", "registration", "deadline", "biometric", "capture",
            "renewal", "renew", "issuance", "document", "documents", "verification",
            "slip", "booklet", "application", "applicant", "expiry", "validity",
        ],
        [Topic.Emergencies] =
        [
            "flood", "flooding", "disaster", "emergency", "evacuation", "evacuate", "warning",
            "alert", "rainfall", "storm", "collapse", "displaced", "relief", "shelter",
            "rescue", "casualty", "casualties", "hazard", "landslide", "fire", "erosion",
            "advisory", "prepare", "preparedness",
        ],
        [Topic.Elections] =
        [
            "election", "elections", "voter", "voters", "register", "registration", "poll",
            "polling", "ballot", "candidate", "party", "constituency", "ward", "collation",
            "result", "results", "pvc", "inec", "campaign", "nomination", "electoral",
            "deadline", "accreditation",
        ],
    };

    /// <summary>The vocabulary a topic is recognised by. Used by ingestion too.</summary>
    public static IReadOnlyList<string> TopicTerms(Topic topic) =>
        TopicVocabulary.TryGetValue(topic, out var terms) ? terms : [];

    /// <summary>A source and why it was selected.</summary>
    public sealed record Match(SourceRecord Source, double Score);

    /// <summary>
    /// Returns the excerpts to check the claim against, best first. An empty
    /// result is a real answer — safety rule 4 turns it into
    /// <c>insufficient_evidence</c> without any model call at all.
    /// </summary>
    public static IReadOnlyList<Match> Retrieve(
        IReadOnlyList<SourceRecord> corpus,
        string claim,
        Jurisdiction? jurisdiction)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        if (string.IsNullOrWhiteSpace(claim))
        {
            return [];
        }

        var claimTerms = Tokenise(claim);
        if (claimTerms.Count == 0)
        {
            return [];
        }

        var claimTopics = InferTopics(claimTerms);

        return [.. corpus
            .Where(source => source.Active)
            .Select(source => new Match(source, Score(source, claimTerms, claimTopics, jurisdiction)))
            .Where(match => match.Score >= RelevanceFloor)
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Source.Id, StringComparer.Ordinal)
            .Take(MaxResults)];
    }

    /// <summary>Which topics a claim's wording points at. May be none, or several.</summary>
    public static IReadOnlyCollection<Topic> InferTopics(IReadOnlyCollection<string> claimTerms)
    {
        ArgumentNullException.ThrowIfNull(claimTerms);

        return [.. TopicVocabulary
            .Where(entry => entry.Value.Any(term => claimTerms.Contains(term)))
            .Select(entry => entry.Key)];
    }

    private static double Score(
        SourceRecord source,
        IReadOnlyCollection<string> claimTerms,
        IReadOnlyCollection<Topic> claimTopics,
        Jurisdiction? jurisdiction)
    {
        var score = 0.0;

        if (claimTopics.Contains(source.Topic))
        {
            score += TopicMatchWeight;
        }

        // A nationwide source can bear on a state-level claim, but not the
        // reverse: a Lagos notice says nothing about anywhere else.
        if (jurisdiction is { } claimed)
        {
            if (source.Jurisdiction == claimed)
            {
                score += JurisdictionMatchWeight;
            }
            else if (source.Jurisdiction == Jurisdiction.Ng)
            {
                score += JurisdictionMatchWeight / 2;
            }
            else
            {
                // A state-specific source for a different state is not evidence
                // about this claim at all.
                return 0;
            }
        }

        var excerptTerms = Tokenise(source.Excerpt);
        score += claimTerms.Count(excerptTerms.Contains) * KeywordWeight;

        var titleTerms = Tokenise($"{source.Title} {source.Issuer}");
        score += claimTerms.Count(titleTerms.Contains) * TitleKeywordWeight;

        return score;
    }

    /// <summary>Lower-cases, splits on non-letters, and drops stop words and very short words.</summary>
    public static HashSet<string> Tokenise(string? text)
    {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(text))
        {
            return tokens;
        }

        var current = new List<char>();

        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                current.Add(char.ToLower(character, CultureInfo.InvariantCulture));
            }
            else if (current.Count > 0)
            {
                Add(tokens, current);
                current.Clear();
            }
        }

        if (current.Count > 0)
        {
            Add(tokens, current);
        }

        return tokens;
    }

    private static void Add(HashSet<string> tokens, List<char> characters)
    {
        var word = new string([.. characters]);

        if (word.Length > 2 && !StopWords.Contains(word))
        {
            tokens.Add(word);
        }
    }
}
