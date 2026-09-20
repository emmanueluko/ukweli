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
    /// The plain-text part.
    /// </summary>
    /// <remarks>
    /// Always sent alongside the HTML. Plenty of people read mail in clients
    /// that show text only, and a sign-in link that works for some readers and
    /// not others is a sign-in link that does not work.
    /// </remarks>
    public static string Text(string linkUrl, TimeSpan validFor) =>
        $"""
        Sign in to Ukweli by opening this link:

        {linkUrl}

        The link works once and expires in {validFor.TotalMinutes:0} minutes.

        If you did not ask to sign in, ignore this email. Nobody can use the
        link without opening it, and no account has been changed.
        """;

    /// <summary>
    /// The HTML part: the mark, a real button, and the address written out
    /// underneath.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Laid out with tables and inline styles because that is what mail clients
    /// render reliably — Outlook in particular ignores most of a stylesheet.
    /// </para>
    /// <para>
    /// The full address appears below the button on purpose. A button hides
    /// where it goes, and this is an email asking someone to sign in; a person
    /// who has been taught to check links before clicking should be able to.
    /// It also survives clients that strip the button entirely.
    /// </para>
    /// </remarks>
    public static string Html(string linkUrl, TimeSpan validFor, string appUrl)
    {
        var logo = $"{appUrl.TrimEnd('/')}/ukweli-icon.png";
        var minutes = $"{validFor.TotalMinutes:0}";
        var safeLink = System.Net.WebUtility.HtmlEncode(linkUrl);

        return $"""
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"></head>
        <body style="margin:0;padding:0;background:#fcfcf9;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background:#fcfcf9;padding:32px 16px;">
            <tr><td align="center">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;background:#ffffff;border-radius:14px;padding:32px 28px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">

                <tr><td align="center" style="padding-bottom:20px;">
                  <img src="{logo}" width="56" height="56" alt="Ukweli"
                       style="display:block;border:0;border-radius:14px;">
                </td></tr>

                <tr><td align="center" style="font-size:22px;font-weight:700;color:#1b1f1c;padding-bottom:8px;">
                  Sign in to Ukweli
                </td></tr>

                <tr><td align="center" style="font-size:15px;line-height:1.55;color:#51604f;padding-bottom:26px;">
                  Tap the button below to finish signing in. No password needed.
                </td></tr>

                <tr><td align="center" style="padding-bottom:22px;">
                  <a href="{safeLink}"
                     style="display:inline-block;background:#1e5b3c;color:#ffffff;font-size:16px;font-weight:600;
                            text-decoration:none;padding:15px 34px;border-radius:10px;">
                    Sign in to Ukweli
                  </a>
                </td></tr>

                <tr><td align="center" style="font-size:13px;line-height:1.5;color:#51604f;padding-bottom:22px;">
                  This link works once and expires in {minutes} minutes.
                </td></tr>

                <tr><td style="border-top:1px solid #edefe8;padding-top:18px;font-size:12px;line-height:1.55;color:#51604f;">
                  If the button does not work, copy this address into your browser:<br>
                  <span style="color:#163f2b;word-break:break-all;">{safeLink}</span>
                </td></tr>

                <tr><td style="padding-top:16px;font-size:12px;line-height:1.55;color:#51604f;">
                  If you did not ask to sign in, ignore this email. Nobody can use the link
                  without opening it, and no account has been changed.
                </td></tr>

              </table>
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:480px;">
                <tr><td align="center" style="padding-top:18px;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;font-size:11px;color:#51604f;">
                  Ukweli — truth for stronger communities
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }
}
