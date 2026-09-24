namespace Vispl.HRManagement.MailReceiver.Smtp;

/// <summary>One message this server accepted, parsed only as far as the inbox needs.</summary>
public sealed record CapturedMessage(
    string Id,
    DateTimeOffset ReceivedUtc,
    string From,
    IReadOnlyList<string> To,
    string Subject,
    string? TextBody,
    string? HtmlBody,

    /// <summary>
    /// Every link found in the message. An activation mail exists to carry one, and clicking
    /// it from the inbox is the whole reason to run this thing locally.
    /// </summary>
    IReadOnlyList<string> Links,

    /// <summary>The bytes as they arrived, for when the parse is not to be trusted.</summary>
    string Raw);
