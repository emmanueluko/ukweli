using Ukweli.Contracts;
using Ukweli.Evidence;

namespace Ukweli.Evidence.Tests;

/// <summary>
/// The guardrails do not stop applying because the words changed language.
/// </summary>
public sealed class TranslationGuardTests
{
    private static readonly Translation English = new(
        Explanation: "The NCDC reported 253 deaths and a case fatality rate of 24.0% across "
            + "24 states, up to 23 August 2026.",
        SimpleExplanation: "Nigeria's disease control centre reported 253 deaths across 24 states.",
        Unknowns:
        [
            "How many deaths have been reported since 23 August 2026.",
            "Whether the message described this year or an earlier outbreak.",
        ],
        Action: "Check the NCDC situation reports page for the most recent week.");

    [Fact]
    public void Accepts_a_faithful_translation()
    {
        var pidgin = new Translation(
            "NCDC talk say 253 people don die, and say 24.0% of confirmed cases dey die, across "
            + "24 states, till 23 August 2026.",
            "Di disease control centre for Nigeria talk say 253 people don die for 24 states.",
            [
                "How many people don die since 23 August 2026.",
                "Whether di message dey talk about dis year or one outbreak wey don pass.",
            ],
            "Check di NCDC situation reports page for di latest week.");

        var result = TranslationGuard.Check(pidgin, Language.NigerianPidgin, English);

        Assert.True(result.Ok);
        Assert.Equal(pidgin, result.Accepted);
    }

    [Fact]
    public void Ordinary_pidgin_is_not_mistaken_for_a_hedge()
    {
        // "e fit don change" is how a Pidgin speaker says a cumulative figure
        // has moved since. Rejecting it would mean no usable Pidgin at all.
        var pidgin = English with
        {
            Explanation = English.Explanation + " Di figures na cumulative, so e fit don change.",
        };

        Assert.True(TranslationGuard.Check(pidgin, Language.NigerianPidgin, English).Ok);
    }

    [Fact]
    public void Rejects_a_translation_that_changes_a_figure()
    {
        var wrong = English with
        {
            Explanation = English.Explanation.Replace("253", "235", StringComparison.Ordinal),
        };

        var result = TranslationGuard.Check(wrong, Language.French, English);

        Assert.False(result.Ok);
        Assert.Contains("figures", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Accepts_a_french_decimal_comma()
    {
        var french = new Translation(
            "Le NCDC a signalé 253 décès et un taux de létalité de 24,0 % dans 24 États, "
            + "jusqu'au 23 août 2026.",
            "Le centre de contrôle des maladies du Nigeria a signalé 253 décès dans 24 États.",
            [
                "Combien de décès ont été signalés depuis le 23 août 2026.",
                "Si le message décrivait cette année ou une épidémie antérieure.",
            ],
            "Consultez la page des rapports de situation du NCDC pour la semaine la plus récente.");

        Assert.True(TranslationGuard.Check(french, Language.French, English).Ok);
    }

    [Fact]
    public void Rejects_a_translation_that_drops_an_unknown()
    {
        // What a check could not establish is part of its answer. A translation
        // that loses one reads more certain than the finding actually is.
        var fewer = English with { Unknowns = [English.Unknowns[0]] };

        var result = TranslationGuard.Check(fewer, Language.NigerianPidgin, English);

        Assert.False(result.Ok);
        Assert.Contains("unknowns", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(Language.NigerianPidgin, "I dey sure say di claim correct.")]
    [InlineData(Language.French, "Nous avons une confiance élevée dans ce résultat.")]
    [InlineData(Language.English, "This carries a high probability of being true.")]
    public void Rejects_a_confidence_score_in_any_language(Language language, string addition)
    {
        // Safety rule 6 has no language. A confidence score smuggled back in
        // through a translation is still a confidence score.
        var hedged = English with { Explanation = English.Explanation + " " + addition };

        var result = TranslationGuard.Check(hedged, language, English);

        Assert.False(result.Ok);
        Assert.Contains("confident", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_percentage_invented_by_the_translation_is_caught_as_a_changed_figure()
    {
        // "90% confident" fails on the figures before it reaches the wording
        // check, which is the stricter of the two and the right one to fire.
        var hedged = English with
        {
            Explanation = English.Explanation + " We are 90% confident.",
        };

        Assert.False(TranslationGuard.Check(hedged, Language.English, English).Ok);
    }

    [Theory]
    [InlineData(Language.NigerianPidgin, "Just pay am make dem no disturb you.")]
    [InlineData(Language.French, "Payez la taxe avant la date limite.")]
    [InlineData(Language.English, "Pay the levy at the account number in the message.")]
    public void Rejects_an_action_that_tells_somebody_to_pay(Language language, string action)
    {
        // Safety rule 7 in each language: a next step may ask somebody to
        // confirm something or to wait, never to hand over money.
        var unsafeAction = English with { Action = action };

        var result = TranslationGuard.Check(unsafeAction, language, English);

        Assert.False(result.Ok);
        Assert.Contains("next step", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rejects_an_empty_translation()
    {
        Assert.False(TranslationGuard.Check(null, Language.French, English).Ok);
        Assert.False(
            TranslationGuard.Check(English with { Action = "  " }, Language.French, English).Ok);
    }

    [Fact]
    public void Share_text_is_written_in_the_readers_language()
    {
        var pidgin = ShareText.Build(
            VerdictStatus.Supported,
            new DateOnly(2026, 8, 23),
            "https://ncdc.gov.ng/sitrep.pdf",
            "https://www.ukweli.online",
            "abc1234567",
            Language.NigerianPidgin);

        // Fixed, hand-written copy — the part most likely to be forwarded is
        // never something a model produced, in any language.
        Assert.Contains("Official source back am", pidgin, StringComparison.Ordinal);
        Assert.Contains("https://www.ukweli.online/r/abc1234567", pidgin, StringComparison.Ordinal);

        var french = ShareText.Build(
            VerdictStatus.InsufficientEvidence, null, null, "https://x.test", "abc1234567",
            Language.French);

        Assert.Contains("Pas assez d'éléments", french, StringComparison.Ordinal);
    }
}
