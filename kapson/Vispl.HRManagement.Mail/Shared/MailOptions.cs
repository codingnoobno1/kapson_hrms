namespace Vispl.HRManagement.Mail.Shared;

/// <summary>
/// How this application reaches the mail sender service. Bound from the <c>Mail</c> section.
///
/// The application does not speak SMTP. It posts a rendered message to the sender service,
/// which owns the relay, the credentials and the retry behaviour — so swapping a development
/// catcher for a real provider is a change to one small service, not to the HR system.
/// </summary>
public sealed class MailOptions
{
    public const string SectionName = "Mail";

    /// <summary>The sender service. 7000 is this host, 7001 sends, 7002 receives.</summary>
    public string SenderBaseUrl { get; set; } = "http://localhost:7001";

    /// <summary>Presented as <c>X-Mail-Key</c>. Blank matches an unsecured local sender.</summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// The origin links in mail point at. Mail is read outside the app, so a relative path is
    /// useless — this is what turns <c>/onboarding</c> into something clickable.
    /// </summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:7000";

    /// <summary>
    /// Where mail this system sent can be read back, when there is somewhere to read it.
    ///
    /// Blank turns the admin inbox off rather than pointing it at nothing — which is the right
    /// answer once a real provider is relaying, because there is no mailbox of ours to browse
    /// and a page that shows an empty list would be lying about it.
    /// </summary>
    public string? ReceiverBaseUrl { get; set; }

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Off stops delivery without removing the configuration, which is what a test run or a
    /// restored backup wants: no mail should leave, and the reason should be a setting somebody
    /// can see rather than an unreachable host.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
