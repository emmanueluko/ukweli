using Ukweli.Data;

namespace Ukweli.Api.Tests;

/// <summary>
/// The schema has to read the way section 4 of the specification writes it:
/// <c>claim_analyses</c>, <c>normalized_claim</c>, <c>source_ids</c>.
/// </summary>
public class SnakeCaseNamingTests
{
    [Theory]
    [InlineData("Source", "source")]
    [InlineData("ClaimAnalysis", "claim_analysis")]
    [InlineData("normalizedClaim", "normalized_claim")]
    [InlineData("SourceType", "source_type")]
    [InlineData("CollectionUrl", "collection_url")]
    [InlineData("ActionIsGeneric", "action_is_generic")]
    [InlineData("SimpleExplanation", "simple_explanation")]
    [InlineData("PK_Sources", "pk_sources")]
    [InlineData("IX_ClaimAnalyses_UserId", "ix_claim_analyses_user_id")]
    public void ConvertsPascalAndCamelCase(string input, string expected)
    {
        Assert.Equal(expected, SnakeCaseNaming.ToSnakeCase(input));
    }

    [Theory]
    [InlineData("URL", "url")]
    [InlineData("SourceURL", "source_url")]
    [InlineData("URLSource", "url_source")]
    public void KeepsRunsOfCapitalsTogether(string input, string expected)
    {
        // "URL" must not become "u_r_l".
        Assert.Equal(expected, SnakeCaseNaming.ToSnakeCase(input));
    }

    [Theory]
    [InlineData("")]
    [InlineData("id")]
    [InlineData("already_snake")]
    public void LeavesNamesThatNeedNoChange(string input)
    {
        Assert.Equal(input, SnakeCaseNaming.ToSnakeCase(input));
    }
}
