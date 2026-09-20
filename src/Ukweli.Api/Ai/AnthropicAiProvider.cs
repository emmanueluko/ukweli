using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;
using Ukweli.Contracts;
using Ukweli.Evidence;

namespace Ukweli.Api.Ai;

/// <summary>
/// The real model, behind <see cref="IAiProvider"/>.
/// </summary>
/// <remarks>
/// <para>
/// The model sees the claim and the retrieved excerpts, and nothing else — no
/// web access, no tools, no way to add a source (safety rule 1). Both calls use
/// structured outputs so the response either parses against the schema or fails
/// outright; a half-parsed verdict is never guessed at.
/// </para>
/// <para>
/// Every failure becomes <see cref="AiUnavailableException"/>, which the
/// endpoint turns into a retryable 503. Safety rule 8: a failed call never
/// produces a fabricated result.
/// </para>
/// </remarks>
public sealed class AnthropicAiProvider(
    AnthropicClient client,
    PromptLibrary prompts,
    UkweliOptions options) : IAiProvider
{
    /// <summary>The model. Recorded on every analysis as part of <c>modelVersion</c>.</summary>
    public const string ModelId = "claude-opus-5";

    /// <summary>Hard timeout for a single call, as the specification requires.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(8);

    private const int MaxTokens = 2048;

    /// <summary>Non-ASCII stays readable in the prompt the model is shown.</summary>
    private static readonly JsonSerializerOptions PayloadOptions =
        new() { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>Identifies which model and prompt produced a stored analysis.</summary>
    public static string VersionFor(string promptVersion) =>
        $"anthropic/{ModelId}@{promptVersion}";

    public async Task<ExtractedClaim> ExtractAsync(
        string claimText, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimText);

        var prompt = PromptLibrary.Fill(
            prompts.Extract,
            new Dictionary<string, string> { ["CLAIM"] = claimText });

        var json = await CallAsync(prompt, ExtractSchema, cancellationToken);

        return ParseExtracted(json);
    }

    public async Task<ProposedVerdict> VerdictAsync(
        ExtractedClaim claim,
        IReadOnlyList<SourceRecord> retrieved,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        ArgumentNullException.ThrowIfNull(retrieved);

        if (retrieved.Count == 0)
        {
            // Safety rule 4: with nothing retrieved there is nothing to ask
            // about, and the call is skipped rather than made.
            throw new InvalidOperationException(
                "VerdictAsync must not be called with an empty retrieval set.");
        }

        var prompt = PromptLibrary.Fill(
            prompts.Verdict,
            new Dictionary<string, string>
            {
                ["CLAIM"] = claim.NormalizedClaim,
                ["JURISDICTION"] = claim.Jurisdiction is { } j
                    ? WireNames.ToWire(j)
                    : "not stated",
                ["EXCERPTS"] = FormatExcerpts(retrieved),
            });

        var json = await CallAsync(prompt, VerdictSchema, cancellationToken);

        return ParseVerdict(json);
    }

    public async Task<Translation> TranslateAsync(
        Translation approved,
        Language language,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(approved);

        // English is the language the result was written and checked in.
        if (language == Language.English)
        {
            return approved;
        }

        var prompt = PromptLibrary.Fill(
            prompts.Translate,
            new Dictionary<string, string>
            {
                ["LANGUAGE"] = LanguageName(language),
                ["PAYLOAD"] = JsonSerializer.Serialize(
                    new
                    {
                        explanation = approved.Explanation,
                        simpleExplanation = approved.SimpleExplanation,
                        unknowns = approved.Unknowns,
                        action = approved.Action,
                    },
                    PayloadOptions),
            });

        var json = await CallAsync(prompt, TranslateSchema, cancellationToken);

        return ParseTranslation(json);
    }

    /// <summary>
    /// How the prompt names a language. Spelled out rather than passed as a
    /// code, because "pcm" means nothing to a reader of the prompt.
    /// </summary>
    private static string LanguageName(Language language) => language switch
    {
        Language.NigerianPidgin => "Nigerian Pidgin",
        Language.French => "French",
        _ => "English",
    };

    internal static Translation ParseTranslation(JsonElement json)
    {
        var translation = new Translation(
            Explanation: String(json, "explanation") ?? string.Empty,
            SimpleExplanation: String(json, "simpleExplanation") ?? string.Empty,
            Unknowns: StringArray(json, "unknowns"),
            Action: String(json, "action") ?? string.Empty);

        if (string.IsNullOrWhiteSpace(translation.Explanation))
        {
            throw new AiUnavailableException("The model returned an empty translation.");
        }

        return translation;
    }

    /// <summary>
    /// Renders the retrieved excerpts. Ids are given verbatim because the model
    /// must cite them exactly, and every id it returns is checked against this
    /// same set afterwards.
    /// </summary>
    private static string FormatExcerpts(IReadOnlyList<SourceRecord> retrieved)
    {
        var builder = new StringBuilder();

        foreach (var source in retrieved)
        {
            builder.AppendLine($"--- id: {source.Id}");
            builder.AppendLine($"    issuer: {source.Issuer}");
            builder.AppendLine($"    type: {WireNames.ToWire(source.SourceType)}");
            builder.AppendLine($"    published: {source.PublishedAt?.ToString("yyyy-MM-dd") ?? "unknown"}");
            builder.AppendLine($"    excerpt: {source.Excerpt}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private async Task<JsonElement> CallAsync(
        string prompt,
        IReadOnlyDictionary<string, JsonElement> schema,
        CancellationToken cancellationToken)
    {
        if (!options.ModelConfigured)
        {
            throw new AiUnavailableException(
                "No Anthropic API key is configured, so free-text claims cannot be checked.");
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        Message response;
        try
        {
            response = await client.Messages.Create(
                new MessageCreateParams
                {
                    Model = ModelId,
                    MaxTokens = MaxTokens,
                    OutputConfig = new OutputConfig
                    {
                        Format = new JsonOutputFormat { Schema = schema },
                    },
                    Messages = [new() { Role = Role.User, Content = prompt }],
                },
                cancellationToken: timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AiUnavailableException(
                $"The analysis model did not respond within {Timeout.TotalSeconds:0} seconds.");
        }
        catch (Exception ex) when (ex is not AiUnavailableException)
        {
            // The underlying reason is named, so an operator can tell a bad key
            // from a network failure from a schema rejection without a debugger.
            // The key itself never appears in an SDK exception message.
            throw new AiUnavailableException(
                $"The analysis model could not be reached: {ex.GetType().Name}: {ex.Message}", ex);
        }

        // A refusal is a real outcome, not a result to salvage.
        if (response.StopReason == "refusal")
        {
            throw new AiUnavailableException(
                "The analysis model declined to process this claim.");
        }

        var text = string.Concat(
            response.Content.Select(block => block.Value).OfType<TextBlock>().Select(b => b.Text));

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new AiUnavailableException("The analysis model returned an empty response.");
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new AiUnavailableException(
                "The analysis model returned something that was not valid JSON.", ex);
        }
    }

    internal static ExtractedClaim ParseExtracted(JsonElement json)
    {
        var normalized = String(json, "normalizedClaim");

        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new AiUnavailableException("The model returned no normalised claim.");
        }

        return new ExtractedClaim(
            NormalizedClaim: normalized.Trim(),
            // An unrecognised jurisdiction becomes null rather than a guess: a
            // wrong one sends the check to the wrong evidence entirely.
            Jurisdiction: WireNames.TryParse<Jurisdiction>(String(json, "jurisdiction"), out var j)
                ? j
                : null,
            EffectiveDate: DateOnly.TryParse(String(json, "effectiveDate"), out var date)
                ? date
                : null,
            AffectedGroup: Blank(String(json, "affectedGroup")),
            RequestedAction: Blank(String(json, "requestedAction")));
    }

    internal static ProposedVerdict ParseVerdict(JsonElement json)
    {
        if (!WireNames.TryParse<VerdictStatus>(String(json, "status"), out var status))
        {
            // A status outside the four is not a verdict Ukweli can stand behind.
            throw new AiUnavailableException(
                $"The model returned an unrecognised verdict: '{String(json, "status")}'.");
        }

        return new ProposedVerdict(
            Status: status,
            Rationale: String(json, "rationale") ?? string.Empty,
            CitedSourceIds: StringArray(json, "citedSourceIds"),
            Unknowns: StringArray(json, "unknowns"),
            Action: String(json, "action") ?? string.Empty,
            SimpleExplanation: String(json, "simpleExplanation") ?? string.Empty);
    }

    private static string? String(JsonElement json, string name) =>
        json.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> StringArray(JsonElement json, string name)
    {
        if (!json.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return [.. value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String)
            .Select(item => item.GetString()!)
            .Where(item => !string.IsNullOrWhiteSpace(item))];
    }

    private static IReadOnlyDictionary<string, JsonElement> ExtractSchema => Schema("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["normalizedClaim", "jurisdiction", "effectiveDate", "affectedGroup", "requestedAction"],
          "properties": {
            "normalizedClaim": { "type": "string" },
            "jurisdiction": {
              "anyOf": [
                { "type": "string", "enum": ["NG", "NG-LA"] },
                { "type": "null" }
              ]
            },
            "effectiveDate": { "type": ["string", "null"] },
            "affectedGroup": { "type": ["string", "null"] },
            "requestedAction": { "type": ["string", "null"] }
          }
        }
        """);

    private static IReadOnlyDictionary<string, JsonElement> VerdictSchema => Schema("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["status", "rationale", "citedSourceIds", "unknowns", "action", "simpleExplanation"],
          "properties": {
            "status": {
              "type": "string",
              "enum": ["supported", "contradicted", "mixed_unclear", "insufficient_evidence"]
            },
            "rationale": { "type": "string" },
            "citedSourceIds": { "type": "array", "items": { "type": "string" } },
            "unknowns": { "type": "array", "items": { "type": "string" } },
            "action": { "type": "string" },
            "simpleExplanation": { "type": "string" }
          }
        }
        """);

    private static IReadOnlyDictionary<string, JsonElement> TranslateSchema => Schema("""
        {
          "type": "object",
          "additionalProperties": false,
          "required": ["explanation", "simpleExplanation", "unknowns", "action"],
          "properties": {
            "explanation": { "type": "string" },
            "simpleExplanation": { "type": "string" },
            "unknowns": { "type": "array", "items": { "type": "string" } },
            "action": { "type": "string" }
          }
        }
        """);

    private static Dictionary<string, JsonElement> Schema(string json)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.Clone());
    }
}
