namespace Vispl.HRManagement.Contracts.Admin;

/// <summary>
/// The mail configuration as the portal shows it.
///
/// <see cref="HasApiKey"/> rather than the key: a page that renders a credential puts it in a
/// browser, in a screenshot and in whatever recorded the session. The portal can say one is
/// set, and can replace it. It cannot show it.
/// </summary>
public sealed record MailConfigurationDto(
    bool Enabled,
    string SenderBaseUrl,
    string? ReceiverBaseUrl,
    string PublicBaseUrl,
    bool HasApiKey,
    int TimeoutSeconds,
    bool Stored,
    DateTimeOffset? UpdatedUtc,
    string? UpdatedBy);

public sealed record SaveMailConfigurationRequest
{
    public bool Enabled { get; init; } = true;
    public string SenderBaseUrl { get; init; } = string.Empty;
    public string? ReceiverBaseUrl { get; init; }
    public string PublicBaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Null leaves the stored key alone; empty clears it. Two different intentions that a
    /// single blank box cannot tell apart, so the page asks which.
    /// </summary>
    public string? ApiKey { get; init; }

    public int TimeoutSeconds { get; init; } = 15;
}

/// <summary>What the sender service says about itself, and what of it can be changed.</summary>
public sealed record MailRelayDto(
    string Service,
    string Host,
    int Port,
    bool UseSsl,
    string? UserName,
    bool HasPassword,
    string FromAddress,
    string FromName,
    string? InboxUrl,
    int TimeoutSeconds,
    int Sent,
    int Failed,
    DateTimeOffset? LastAttemptUtc,
    string? LastFailure);

public sealed record SaveMailRelayRequest
{
    public string Host { get; init; } = string.Empty;
    public int Port { get; init; } = 25;
    public bool UseSsl { get; init; }
    public string? UserName { get; init; }

    /// <summary>Null keeps the relay's current password; empty clears it.</summary>
    public string? Password { get; init; }

    public string FromAddress { get; init; } = string.Empty;
    public string FromName { get; init; } = string.Empty;
    public string? InboxUrl { get; init; }
    public int TimeoutSeconds { get; init; } = 15;
}

/// <summary>Whether something worked, and what to say if it did not.</summary>
public sealed record MailConfigurationResult(bool Ok, string? Failure = null)
{
    public static MailConfigurationResult Success() => new(true);
    public static MailConfigurationResult Failed(string reason) => new(false, reason);
}
