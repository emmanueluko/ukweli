using Ukweli.Data;

namespace Ukweli.Api.Tests;

public class MagicLinkServiceTests
{
    [Theory]
    [InlineData("someone@example.com")]
    [InlineData("first.last+tag@sub.example.co.uk")]
    public void AcceptsAnAddressThatCouldReceiveMail(string email)
    {
        Assert.True(Auth.MagicLinkService.LooksLikeEmail(email));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("no-at-sign")]
    [InlineData("@example.com")]
    [InlineData("someone@")]
    [InlineData("two@@example.com")]
    [InlineData("someone@nodot")]
    [InlineData("has space@example.com")]
    public void RejectsWhatCannotBeAnAddress(string? email)
    {
        Assert.False(Auth.MagicLinkService.LooksLikeEmail(email));
    }

    [Theory]
    [InlineData("Someone@Example.COM", "someone@example.com")]
    [InlineData("  someone@example.com  ", "someone@example.com")]
    public void NormalisesTheAddressSoOneAccountMeansOnePerson(string input, string expected)
    {
        // Without this, Someone@ and someone@ would be two accounts.
        Assert.Equal(expected, Auth.MagicLinkService.NormaliseEmail(input));
    }

    [Fact]
    public void TokensAreUnpredictableAndUrlSafe()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => Tokens.New()).ToList();

        Assert.Equal(tokens.Count, tokens.Distinct(StringComparer.Ordinal).Count());
        Assert.All(tokens, token => Assert.DoesNotContain('+', token));
        Assert.All(tokens, token => Assert.DoesNotContain('/', token));
        Assert.All(tokens, token => Assert.DoesNotContain('=', token));
        Assert.All(tokens, token => Assert.True(token.Length >= 40));
    }

    [Fact]
    public void TheStoredHashIsNotTheToken()
    {
        // A database backup must not contain anything that can sign anyone in.
        var token = Tokens.New();
        var hash = Tokens.Hash(token);

        Assert.NotEqual(token, hash);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, Tokens.Hash(token));
        Assert.NotEqual(hash, Tokens.Hash(Tokens.New()));
    }
}
