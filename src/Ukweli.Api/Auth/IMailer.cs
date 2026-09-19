namespace Ukweli.Api.Auth;

/// <summary>Sends the sign-in link. Behind an interface so dev, production and tests differ only here.</summary>
public interface IMailer
{
    Task SendMagicLinkAsync(string email, string linkUrl, CancellationToken cancellationToken);
}

/// <summary>The one email Ukweli sends.</summary>
public static class MagicLinkEmail
{
    public const string Subject = "Your Ukweli sign-in link";

    /// <summary>
    /// Deliberately plain. It states what the link does, how long it lasts, and
    /// what to do if the recipient did not ask for it — an unexpected sign-in
    /// email is the first sign someone is trying to get into your account.
    /// </summary>
    public static string Body(string linkUrl, TimeSpan validFor) =>
        $"""
        Sign in to Ukweli by opening this link:

        {linkUrl}

        The link works once and expires in {validFor.TotalMinutes:0} minutes.

        If you did not ask to sign in, ignore this email. Nobody can use the
        link without opening it, and no account has been changed.
        """;
}
