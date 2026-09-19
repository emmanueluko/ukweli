using System.Net.Mail;

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
            Body = MagicLinkEmail.Body(linkUrl, MagicLinkService.ValidFor),
            IsBodyHtml = false,
        };

        await client.SendMailAsync(message, cancellationToken);

        // The address is not logged: who asked to sign in is not something the
        // logs need to know.
        logger.MagicLinkSent(options.SmtpHost ?? "localhost");
    }
}
