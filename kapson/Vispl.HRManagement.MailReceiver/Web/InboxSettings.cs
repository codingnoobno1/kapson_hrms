using Microsoft.Extensions.Options;
using Vispl.HRManagement.MailReceiver.Imap;
using Vispl.HRManagement.MailReceiver.Pop3;
using Vispl.HRManagement.MailReceiver.Smtp;

namespace Vispl.HRManagement.MailReceiver.Web;

/// <summary>How to point something at this server. Every number in it is the configured one.</summary>
public sealed record InboxSettings(
    IReadOnlyList<ProtocolEndpoint> Protocols,
    SenderSummary Sender,

    /// <summary>Stated once here rather than repeated in three places that can disagree.</summary>
    string Encryption,
    string Credentials,
    string Folder)
{
    /// <summary>
    /// Read straight off the options the listeners themselves bind with.
    ///
    /// The viewer used to carry <c>localhost:1143</c> in its markup, which was a guess that
    /// happened to be right on the machine it was written on. A panel that tells somebody which
    /// port to type is worth nothing unless it is the port that is actually open.
    /// </summary>
    public static InboxSettings From(
        IOptions<SmtpOptions> smtp,
        IOptions<Pop3Options> pop,
        IOptions<ImapOptions> imap,
        IOptions<ReceiverOptions> receiver)
    {
        var settings = receiver.Value;

        Uri? sender = Uri.TryCreate(settings.SenderBaseUrl, UriKind.Absolute, out var parsed) ? parsed : null;

        return new InboxSettings(
            [
                new ProtocolEndpoint("IMAP", settings.ResolveClientHost(imap.Value.ListenOnAllInterfaces),
                    imap.Value.Port, "Browse from a mail client without downloading."),
                new ProtocolEndpoint("POP3", settings.ResolveClientHost(pop.Value.ListenOnAllInterfaces),
                    pop.Value.Port, "Download into a mail client."),
                new ProtocolEndpoint("SMTP in", settings.ResolveClientHost(smtp.Value.ListenOnAllInterfaces),
                    smtp.Value.Port, "Where mail is delivered to this server.")
            ],
            new SenderSummary(settings.SenderBaseUrl, sender?.Authority ?? settings.SenderBaseUrl, sender is not null),
            Encryption: "No encryption",
            Credentials: "Any username and password",
            Folder: "INBOX");
    }
}

public sealed record ProtocolEndpoint(string Name, string Host, int Port, string Purpose)
{
    public string Address => $"{Host}:{Port}";
}

/// <summary>
/// Where Compose is relayed. <see cref="Authority"/> exists so the page can say where a message
/// went without printing a port number somebody typed into a string a year ago.
/// </summary>
public sealed record SenderSummary(string BaseUrl, string Authority, bool Configured);
