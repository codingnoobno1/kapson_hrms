using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vispl.HRManagement.BM.ITF.Shared;

namespace Vispl.HRManagement.Mail.Shared;

/// <summary>
/// Reads the mailbox over the receiver's JSON API.
///
/// The mirror of <see cref="MailServiceEmailSender"/>. The HR system does not speak IMAP any
/// more than it speaks SMTP: it asks a small service that owns the protocols, which is what
/// keeps the admin inbox page a page rather than a mail client implementation.
///
/// Nothing here throws. A mail service being down is a normal Tuesday, and an administrator
/// opening a page deserves a sentence about it rather than an error screen.
/// </summary>
public sealed class MailInboxClient(
    HttpClient http,
    IOptions<MailOptions> options,
    ILogger<MailInboxClient> logger) : IMailInbox
{
    private readonly MailOptions settings = options.Value;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(settings.ReceiverBaseUrl);

    public string Origin => settings.ReceiverBaseUrl ?? string.Empty;

    public Task<MailInboxResult<IReadOnlyList<MailboxSummary>>> ListAsync(
        string? search = null, string? toAddress = null, CancellationToken cancellationToken = default)
    {
        var query = new List<string>();

        // `to` and `q` are separate on the receiver on purpose: asking for one address should
        // never match a body that happens to mention it.
        if (!string.IsNullOrWhiteSpace(toAddress)) query.Add("to=" + Uri.EscapeDataString(toAddress));
        if (!string.IsNullOrWhiteSpace(search)) query.Add("q=" + Uri.EscapeDataString(search));

        var path = "api/inbox/v1/messages" + (query.Count > 0 ? "?" + string.Join("&", query) : "");

        return SendAsync<IReadOnlyList<MailboxSummary>>(
            HttpMethod.Get, path, [], cancellationToken);
    }

    public Task<MailInboxResult<MailboxMessage?>> FindAsync(
        string id, CancellationToken cancellationToken = default) =>
        SendAsync<MailboxMessage?>(
            HttpMethod.Get, $"api/inbox/v1/messages/{Uri.EscapeDataString(id)}", null, cancellationToken);

    public Task<MailInboxResult<MailboxState>> GetStateAsync(CancellationToken cancellationToken = default) =>
        SendAsync<MailboxState>(HttpMethod.Get, "api/inbox/v1/state", new MailboxState(0, 0), cancellationToken);

    public Task<MailInboxResult<bool>> SetSeenAsync(
        string id, bool seen, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Post,
            $"api/inbox/v1/messages/{Uri.EscapeDataString(id)}/{(seen ? "read" : "unread")}",
            true, cancellationToken, expectsBody: false);

    public Task<MailInboxResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken = default) =>
        SendAsync<bool>(
            HttpMethod.Delete, $"api/inbox/v1/messages/{Uri.EscapeDataString(id)}",
            true, cancellationToken, expectsBody: false);

    /// <summary>
    /// One place where a call is made and a failure is turned into an answer.
    ///
    /// <paramref name="whenEmpty"/> is what a 404 means for that call — an empty list, a message
    /// that is not there, a delete that had nothing to delete. A missing message is not a fault
    /// and should not read like one.
    /// </summary>
    private async Task<MailInboxResult<T>> SendAsync<T>(
        HttpMethod method,
        string path,
        T? whenEmpty,
        CancellationToken cancellationToken,
        bool expectsBody = true)
    {
        if (!IsConfigured)
        {
            return MailInboxResult<T>.Unreachable("No mail inbox is configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(method, new Uri(new Uri(settings.ReceiverBaseUrl!), path));

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Mail-Key", settings.ApiKey);
            }

            using var response = await http.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return MailInboxResult<T>.Ok(whenEmpty!);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "The mail inbox answered {Status} for {Path}.", (int)response.StatusCode, path);

                return MailInboxResult<T>.Unreachable($"The mail inbox answered {(int)response.StatusCode}.");
            }

            if (!expectsBody)
            {
                return MailInboxResult<T>.Ok(whenEmpty!);
            }

            var value = await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken);

            return MailInboxResult<T>.Ok(value ?? whenEmpty!);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception,
                "Could not reach the mail inbox at {BaseUrl}.", settings.ReceiverBaseUrl);

            return MailInboxResult<T>.Unreachable(
                $"Could not reach the mail inbox at {settings.ReceiverBaseUrl}.");
        }
    }
}
