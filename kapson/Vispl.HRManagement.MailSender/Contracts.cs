using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.MailSender;

/// <summary>
/// What a caller posts to have a message delivered.
///
/// Deliberately no template names and no data to merge: this service renders nothing. The
/// application that knows about candidates and offers composes the words; this one only puts
/// them on the wire. That is what makes it liftable — it has no idea what an HR system is.
/// </summary>
public sealed class SendMailRequest
{
    [Required, EmailAddress, StringLength(256)]
    public string To { get; set; } = string.Empty;

    [StringLength(160)]
    public string? ToName { get; set; }

    [Required, StringLength(300)]
    public string Subject { get; set; } = string.Empty;

    /// <summary>Required. A message with only HTML is a message some readers cannot read.</summary>
    [Required, StringLength(20000)]
    public string Text { get; set; } = string.Empty;

    [StringLength(200000)]
    public string? Html { get; set; }

    /// <summary>
    /// Overrides the configured sender. Present so one relay can serve several clients, each
    /// appearing as itself.
    /// </summary>
    [EmailAddress, StringLength(256)]
    public string? FromAddress { get; set; }

    [StringLength(160)]
    public string? FromName { get; set; }
}

public sealed record SendMailResponse(
    bool Sent,
    string MessageId,
    DateTimeOffset AcceptedUtc,
    string Relay,
    string? Failure = null);

public sealed record MailSenderStatus(
    string Service,
    string RelayHost,
    int RelayPort,
    bool UseSsl,
    string FromAddress,
    int Sent,
    int Failed,
    DateTimeOffset? LastAttemptUtc,
    string? LastFailure,

    /// <summary>Where the relayed mail can be read, when the relay has somewhere to read it.</summary>
    string? InboxUrl);
