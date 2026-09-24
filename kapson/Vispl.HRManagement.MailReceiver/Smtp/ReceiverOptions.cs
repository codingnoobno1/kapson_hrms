using System.Net;

namespace Vispl.HRManagement.MailReceiver.Smtp;

public sealed class ReceiverOptions
{
    public const string SectionName = "Receiver";

    /// <summary>
    /// The sender service, so Compose can post through this server instead of from the
    /// browser. One origin means no CORS to configure and no second host for somebody to get
    /// wrong — the mail app is one app, the way a mail app should be.
    /// </summary>
    public string SenderBaseUrl { get; set; } = "http://localhost:7001";

    /// <summary>Presented to the sender as <c>X-Mail-Key</c> when it wants one.</summary>
    public string? SenderApiKey { get; set; }

    /// <summary>
    /// The hostname to hand a mail client. Blank resolves it: loopback when the listeners are
    /// bound to loopback, this machine's name when they are on the network. Set it only when
    /// neither is right — behind a container or a port forward, where this process cannot know
    /// the name somebody else reaches it by.
    /// </summary>
    public string? ClientHost { get; set; }

    /// <summary>
    /// The host a mail client should connect to, given how the listeners are bound. Answered
    /// here rather than written into the page, so the panel tells the truth on a machine where
    /// the receiver is not on localhost.
    /// </summary>
    public string ResolveClientHost(bool listeningOnAllInterfaces) =>
        !string.IsNullOrWhiteSpace(ClientHost) ? ClientHost.Trim()
            : listeningOnAllInterfaces ? Dns.GetHostName()
            : "localhost";
}
