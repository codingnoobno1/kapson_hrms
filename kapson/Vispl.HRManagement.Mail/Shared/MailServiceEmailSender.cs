using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vispl.HRManagement.BM.ITF.Shared;

namespace Vispl.HRManagement.Mail.Shared;

/// <summary>
/// Hands a rendered message to the mail sender service over HTTP.
///
/// The HR system deliberately does not speak SMTP. Relays, credentials, TLS and whatever a
/// client's provider demands all live in one small service that knows nothing about HR, which
/// is what lets that service be replaced or lifted out without touching anything here.
/// </summary>
public sealed class MailServiceEmailSender(
    HttpClient http,
    IOptions<MailOptions> options,
    ILogger<MailServiceEmailSender> logger) : IEmailSender
{
    private readonly MailOptions settings = options.Value;

    public bool IsConfigured => settings.Enabled && !string.IsNullOrWhiteSpace(settings.SenderBaseUrl);

    public async Task<EmailSendResult> SendAsync(
        EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            // Not an error. Somebody switched mail off or has not set it up, and the caller is
            // entitled to know which rather than watching a request time out.
            return EmailSendResult.Failed("Mail is not configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, new Uri(new Uri(settings.SenderBaseUrl), "api/mail/v1/send"))
            {
                Content = JsonContent.Create(new
                {
                    to = message.ToAddress,
                    toName = message.ToName,
                    subject = message.Subject,
                    text = message.PlainTextBody,
                    html = message.HtmlBody
                })
            };

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Mail-Key", settings.ApiKey);
            }

            using var response = await http.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Handed \"{Subject}\" for {Recipient} to the mail sender.",
                    message.Subject, message.ToAddress);

                return EmailSendResult.Ok();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            logger.LogError(
                "The mail sender refused \"{Subject}\" for {Recipient}: {Status} {Body}",
                message.Subject, message.ToAddress, (int)response.StatusCode, body);

            return EmailSendResult.Failed($"The mail sender answered {(int)response.StatusCode}.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Never rethrown. Every send here is a side effect of something that already
            // succeeded, and losing that because a mail service was down is the wrong trade.
            logger.LogError(exception,
                "Could not reach the mail sender at {BaseUrl}.", settings.SenderBaseUrl);

            return EmailSendResult.Failed(exception.Message);
        }
    }
}
