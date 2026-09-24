namespace Vispl.HRManagement.MailSender;

/// <summary>
/// Where this service hands mail on to. Bound from the <c>Relay</c> configuration section.
///
/// The default is the local receiver on 2525, not a real provider. A mail service that ships
/// with a working path to the outside world is one deployment mistake away from posting
/// candidate addresses somewhere nobody intended, so the safe destination is the default and
/// the real one is a deliberate change.
/// </summary>
public sealed class RelayOptions
{
    public const string SectionName = "Relay";

    public string Host { get; set; } = "localhost";

    /// <summary>The development receiver's SMTP port. A real relay is usually 587.</summary>
    public int Port { get; set; } = 2525;

    public bool UseSsl { get; set; }

    public string? UserName { get; set; }
    public string? Password { get; set; }

    public string FromAddress { get; set; } = "no-reply@vispl-hr.local";
    public string FromName { get; set; } = "Vispl HR";

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Where relayed mail can be read, when the relay is a development receiver that has a
    /// viewer. Blank hides the links, which is the right answer for a real provider: there is
    /// no page to send somebody to, and a link to one that does not exist is worse than none.
    /// </summary>
    public string? InboxUrl { get; set; }

    /// <summary>
    /// A shared secret callers present as <c>X-Mail-Key</c>. Blank leaves the service open,
    /// which is fine on a developer's machine and is refused when it is not one — see
    /// Program.cs, which will not start unauthenticated outside Development.
    /// </summary>
    public string? ApiKey { get; set; }
}
