namespace Vispl.HRManagement.BM.ITF.Shared;

/// <summary>One message as the inbox list needs it. No bodies: the list never shows them.</summary>
public sealed record MailboxSummary(
    string Id,
    DateTimeOffset ReceivedUtc,
    string From,
    IReadOnlyList<string> To,
    string Subject,
    IReadOnlyList<string> Links,
    bool Seen);

/// <summary>One message, opened.</summary>
public sealed record MailboxMessage(
    string Id,
    DateTimeOffset ReceivedUtc,
    string From,
    IReadOnlyList<string> To,
    string Subject,
    string? TextBody,
    string? HtmlBody,
    IReadOnlyList<string> Links,

    /// <summary>The bytes as they arrived, for when the parse is not to be trusted.</summary>
    string Raw);

public sealed record MailboxState(int Count, int Unseen);

/// <summary>
/// A result that can say "I could not reach it" without throwing.
///
/// The inbox is a separate service. It being down is a normal Tuesday, not an exception —
/// a page that shows a message about it beats one that shows a stack trace.
/// </summary>
public sealed record MailInboxResult<T>(bool Reached, T? Value, string? Failure)
{
    public static MailInboxResult<T> Ok(T value) => new(true, value, null);
    public static MailInboxResult<T> Unreachable(string reason) => new(false, default, reason);
}

/// <summary>
/// Reads the mail this system sent, from wherever it was delivered.
///
/// The counterpart to <see cref="IEmailSender"/>: that one hands a message over and forgets it,
/// this one goes and looks. Both are ports, so a development catcher and a real archive are the
/// same shape to everything above — the admin inbox page does not know which it is talking to,
/// which is what stops it growing a dependency on a development tool.
///
/// Like the sender, nothing here throws.
/// </summary>
public interface IMailInbox
{
    /// <summary>False when no inbox is configured, so a page can say so rather than sit empty.</summary>
    bool IsConfigured { get; }

    /// <summary>Where it is reading from. Shown to an administrator, who is entitled to know.</summary>
    string Origin { get; }

    Task<MailInboxResult<IReadOnlyList<MailboxSummary>>> ListAsync(
        string? search = null, string? toAddress = null, CancellationToken cancellationToken = default);

    Task<MailInboxResult<MailboxMessage?>> FindAsync(string id, CancellationToken cancellationToken = default);

    Task<MailInboxResult<MailboxState>> GetStateAsync(CancellationToken cancellationToken = default);

    Task<MailInboxResult<bool>> SetSeenAsync(string id, bool seen, CancellationToken cancellationToken = default);

    Task<MailInboxResult<bool>> DeleteAsync(string id, CancellationToken cancellationToken = default);
}
