using System.Net.Mail;
using System.Net.Mime;

namespace Ukweli.Api.Auth;

/// <summary>
/// Sends over SMTP, which in development means Mailpit on port 1025.
/// </summary>
/// <remarks>
/// Mailpit accepts anything and delivers nothing onward, so a developer can
/// click a real sign-in link without a real mailbox and without any risk of
/// mail reaching an actual address.
/// </remarks>
public sealed class SmtpMailer(UkweliOptions options, ILogger<SmtpMailer> logger) : IMailer
{
    public const string FromAddress = "no-reply@ukweli.local";

    public async Task SendMagicLinkAsync(
        string email, string linkUrl, CancellationToken cancellationToken)
    {
        using var client = new SmtpClient(
            options.SmtpHost ?? "localhost",
            options.SmtpPort ?? 1025);

        using var message = new MailMessage(FromAddress, email)
        {
            Subject = MagicLinkEmail.Subject,
            // The plain-text part is the body; the HTML rides alongside it, so a
            // text-only client still gets a working link.
            Body = MagicLinkEmail.Text(linkUrl, MagicLinkService.ValidFor),
            IsBodyHtml = false,
        };

        message.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
            MagicLinkEmail.Html(linkUrl, MagicLinkService.ValidFor, options.AppUrl),
            null,
            "text/html"));

        await client.SendMailAsync(message, cancellationToken);

        // The address is not logged: who asked to sign in is not something the
        // logs need to know.
        logger.MagicLinkSent(options.SmtpHost ?? "localhost");
    }
}
