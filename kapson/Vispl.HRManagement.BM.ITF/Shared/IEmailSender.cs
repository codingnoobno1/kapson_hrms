namespace Vispl.HRManagement.BM.ITF.Shared;

/// <summary>One message, already rendered. Nothing here knows how it will be delivered.</summary>
public sealed record EmailMessage(
    string ToAddress,
    string ToName,
    string Subject,

    /// <summary>The readable version. Always sent, so a text-only client is never given nothing.</summary>
    string PlainTextBody,

    string? HtmlBody = null);

public sealed record EmailSendResult(bool Sent, string? Failure = null)
{
    public static EmailSendResult Ok() => new(true);
    public static EmailSendResult Failed(string reason) => new(false, reason);
}

/// <summary>
/// Delivers a message, or reports honestly that it could not.
///
/// It never throws. Everything that sends mail here is a side effect of something more
/// important — an application was accepted, an account was created — and losing that because a
/// mail server was unreachable would be the wrong trade every time. Callers decide what a
/// failed send means; most of them log it and carry on.
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// False when no transport is configured, so a caller can say "mail is not set up" rather
    /// than pretending a message is on its way.
    /// </summary>
    bool IsConfigured { get; }
}
