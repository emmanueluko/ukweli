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
        var root = appUrl.TrimEnd('/');
        var logo = $"{root}/ukweli-icon.png";
        var minutes = $"{validFor.TotalMinutes:0}";
        var safeLink = System.Net.WebUtility.HtmlEncode(linkUrl);

        // Georgia stands in for the brand's Fraunces: a web font in an email is
        // blocked or ignored by most clients, and a serif that is present
        // everywhere reads closer to the brand than a fallback sans would.
        const string Serif = "Georgia,'Times New Roman',serif";
        const string Sans = "-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif";

        return $"""
        <!doctype html>
        <html lang="en">
        <head>
          <meta charset="utf-8">
          <meta name="viewport" content="width=device-width,initial-scale=1">
          <meta name="color-scheme" content="light">
          <title>Sign in to Ukweli</title>
        </head>
        <body style="margin:0;padding:0;background:#fcfcf9;-webkit-font-smoothing:antialiased;">

          <!-- The line inboxes show beside the subject. Without it they show the
               first words of the body, which here would be the brand name twice. -->
          <div style="display:none;max-height:0;overflow:hidden;opacity:0;">
            Your sign-in link — it works once and expires in {minutes} minutes.
          </div>

          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"
                 style="background:#fcfcf9;">
            <tr><td align="center" style="padding:32px 16px;">

              <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0"
                     style="max-width:480px;">

                <!-- Masthead -->
                <tr><td align="center" style="padding-bottom:24px;">
                  <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                    <tr>
                      <td style="padding-right:10px;">
                        <img src="{logo}" width="40" height="40" alt=""
                             style="display:block;border:0;border-radius:10px;">
                      </td>
                      <td style="font-family:{Serif};font-size:23px;font-weight:700;color:#163f2b;
                                 letter-spacing:-0.01em;">
                        Ukweli
                      </td>
                    </tr>
                  </table>
                </td></tr>

                <!-- Card -->
                <tr><td style="background:#ffffff;border:1px solid #edefe8;border-radius:16px;
                               padding:40px 32px;">

                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                    <tr><td align="center" style="font-family:{Serif};font-size:27px;line-height:1.25;
                                                  font-weight:600;color:#1b1f1c;padding-bottom:12px;">
                      Sign in to Ukweli
                    </td></tr>

                    <tr><td align="center" style="font-family:{Sans};font-size:15px;line-height:1.6;
                                                  color:#51604f;padding-bottom:30px;">
                      Tap the button below and you are in. There is no password to remember.
                    </td></tr>

                    <tr><td align="center" style="padding-bottom:20px;">
                      <table role="presentation" cellpadding="0" cellspacing="0" border="0">
                        <tr><td align="center" bgcolor="#1e5b3c" style="border-radius:10px;">
                          <a href="{safeLink}"
                             style="display:inline-block;font-family:{Sans};font-size:16px;
                                    font-weight:600;color:#ffffff;text-decoration:none;
                                    padding:16px 40px;border-radius:10px;">
                            Sign in to Ukweli
                          </a>
                        </td></tr>
                      </table>
                    </td></tr>

                    <tr><td align="center" style="font-family:{Sans};font-size:13px;line-height:1.5;
                                                  color:#51604f;padding-bottom:28px;">
                      Works once &middot; expires in {minutes} minutes
                    </td></tr>

                    <tr><td style="border-top:1px solid #edefe8;padding-top:22px;
                                   font-family:{Sans};font-size:12px;line-height:1.6;color:#51604f;">
                      <strong style="color:#1b1f1c;font-weight:600;">Button not working?</strong><br>
                      Copy this address into your browser:<br>
                      <span style="color:#163f2b;word-break:break-all;">{safeLink}</span>
                    </td></tr>
                  </table>

                </td></tr>

                <!-- A person who did not request this needs to know nothing has happened. -->
                <tr><td style="padding:22px 8px 0 8px;font-family:{Sans};font-size:12px;
                               line-height:1.6;color:#51604f;">
                  If you did not ask to sign in, you can ignore this email. The link cannot be
                  used unless someone opens it, and nothing about your account has changed.
                </td></tr>

                <tr><td align="center" style="padding-top:26px;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" border="0">
                    <tr><td align="center" style="border-top:1px solid #dce1d6;padding-top:18px;
                                                  font-family:{Sans};font-size:11px;line-height:1.6;
                                                  color:#51604f;letter-spacing:0.04em;
                                                  text-transform:uppercase;">
                      Ukweli &middot; Truth for stronger communities
                    </td></tr>
                    <tr><td align="center" style="padding-top:8px;font-family:{Sans};font-size:11px;
                                                  line-height:1.6;color:#51604f;">
                      Evidence-backed civic information &middot; not for emergencies
                    </td></tr>
                  </table>
                </td></tr>

              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
    }
}
