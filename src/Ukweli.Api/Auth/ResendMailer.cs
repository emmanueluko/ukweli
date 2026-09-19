using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Ukweli.Api.Auth;

/// <summary>Sends through Resend, used when <c>RESEND_API_KEY</c> is set.</summary>
public sealed class ResendMailer(
    HttpClient http, UkweliOptions options, ILogger<ResendMailer> logger) : IMailer
{
    private const string Endpoint = "https://api.resend.com/emails";

    public async Task SendMagicLinkAsync(
        string email, string linkUrl, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = JsonContent.Create(new
            {
                from = $"Ukweli <no-reply@{new Uri(options.AppUrl).Host}>",
                to = new[] { email },
                subject = MagicLinkEmail.Subject,
                text = MagicLinkEmail.Body(linkUrl, MagicLinkService.ValidFor),
            }),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ResendApiKey);

        using var response = await http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // The caller still answers 202: whether an address exists, and
            // whether its mail was accepted, are not things a stranger should
            // be able to learn by watching status codes.
            logger.MagicLinkFailed((int)response.StatusCode);
            return;
        }

        logger.MagicLinkSent("resend");
    }
}
