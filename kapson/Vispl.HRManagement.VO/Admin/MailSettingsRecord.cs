namespace Vispl.HRManagement.VO.Admin;

/// <summary>
/// How this installation sends and reads mail, as stored.
///
/// One row. Mail is platform infrastructure rather than a per-client setting: there is one
/// relay and one mailbox behind it, and a client that needs its own sending address gets it
/// from the from-address on a message, not from a second relay.
/// </summary>
public sealed class MailSettingsRecord
{
    public long Id { get; set; }

    public bool Enabled { get; set; } = true;

    public required string SenderBaseUrl { get; set; }

    /// <summary>Blank turns the admin inbox off. There is nothing of ours to read.</summary>
    public string? ReceiverBaseUrl { get; set; }

    public required string PublicBaseUrl { get; set; }

    /// <summary>
    /// Protected at rest, never stored as typed. It is a credential for the mail service, and a
    /// credential sitting in a table in plain text is one backup away from being everybody's.
    /// </summary>
    public string? ProtectedApiKey { get; set; }

    public int TimeoutSeconds { get; set; } = 15;

    public DateTimeOffset UpdatedUtc { get; set; }

    /// <summary>Who changed it. Mail configuration decides where candidate addresses go.</summary>
    public string? UpdatedBy { get; set; }
}
