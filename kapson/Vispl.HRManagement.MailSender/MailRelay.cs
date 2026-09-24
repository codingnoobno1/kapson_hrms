using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Vispl.HRManagement.MailSender;

/// <summary>
/// Puts a message on the wire and counts what happened.
///
/// <see cref="SmtpClient"/> rather than a library: the destinations are the development
/// receiver and, later, whatever relay the client provides, and both speak ordinary SMTP. If a
/// provider API or OAuth is ever needed this class is replaced rather than extended.
/// </summary>
public sealed class MailRelay(IOptions<RelayOptions> options, ILogger<MailRelay> logger)
{
    private readonly RelayOptions settings = options.Value;
    private readonly Lock counters = new();

    private int sent;
    private int failed;
    private DateTimeOffset? lastAttemptUtc;
    private string? lastFailure;

    public async Task<SendMailResponse> SendAsync(SendMailRequest request, CancellationToken ct)
    {
        // Generated here rather than taken from the caller: it identifies this delivery
        // attempt, and a caller-supplied one could collide or be forged.
        var messageId = Guid.NewGuid().ToString("n");
        var relay = $"{settings.Host}:{settings.Port}";
        var now = DateTimeOffset.UtcNow;

        try
        {
            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.UseSsl,
                Timeout = settings.TimeoutSeconds * 1000
            };

            // Anonymous unless credentials are configured. The development receiver accepts
            // anything, and an empty username against a real relay is a confusing failure.
            if (!string.IsNullOrWhiteSpace(settings.UserName))
            {
                client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
            }

            using var mail = new MailMessage
            {
                From = new MailAddress(
                    request.FromAddress ?? settings.FromAddress,
                    request.FromName ?? settings.FromName),
                Subject = request.Subject,
                Body = request.Text,
                IsBodyHtml = false
            };

            mail.Headers.Add("X-Message-Id", messageId);
            mail.To.Add(new MailAddress(request.To, request.ToName ?? request.To));

            // Both parts, always. A reader that shows only text still gets the link, which for
            // an activation mail is the entire content of the message.
            if (!string.IsNullOrWhiteSpace(request.Html))
            {
                mail.AlternateViews.Add(
                    AlternateView.CreateAlternateViewFromString(request.Html, null, "text/html"));
            }

            await client.SendMailAsync(mail, ct);

            Record(success: true, null, now);

            logger.LogInformation(
                "Relayed {MessageId} \"{Subject}\" to {Recipient} via {Relay}.",
                messageId, request.Subject, request.To, relay);

            return new SendMailResponse(true, messageId, now, relay);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Record(success: false, exception.Message, now);

            logger.LogError(exception,
                "Could not relay {MessageId} \"{Subject}\" to {Recipient} via {Relay}.",
                messageId, request.Subject, request.To, relay);

            // Reported, not thrown. The caller decides what a failed send costs it; for most
            // of them the answer is a log line, because the thing the mail was about has
            // already happened.
            return new SendMailResponse(false, messageId, now, relay, exception.Message);
        }
    }

    public MailSenderStatus Status()
    {
        lock (counters)
        {
            return new MailSenderStatus(
                "Vispl HR mail sender",
                settings.Host,
                settings.Port,
                settings.UseSsl,
                settings.FromAddress,
                sent,
                failed,
                lastAttemptUtc,
                lastFailure,
                string.IsNullOrWhiteSpace(settings.InboxUrl) ? null : settings.InboxUrl);
        }
    }

    private void Record(bool success, string? failure, DateTimeOffset at)
    {
        lock (counters)
        {
            if (success)
            {
                sent++;
            }
            else
            {
                failed++;
                lastFailure = failure;
            }

            lastAttemptUtc = at;
        }
    }
}
