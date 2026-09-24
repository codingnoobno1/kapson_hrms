namespace Vispl.HRManagement.DL.Shared;

/// <summary>
/// One delivered notification for one recipient. Fan-out happens on write: raising an event
/// for five people creates five rows, so read is a single indexed query and marking one as
/// read cannot affect anybody else.
/// </summary>
public sealed class NotificationEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;

    /// <summary>Identity account this was delivered to. Scalar, not a navigation property.</summary>
    public string RecipientId { get; set; } = string.Empty;

    /// <summary>Recruitment, Onboarding, Interview, Leave, Payroll or System.</summary>
    public string Category { get; set; } = "System";

    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>Normal or High. High is surfaced first and is eligible for push.</summary>
    public string Priority { get; set; } = "Normal";

    /// <summary>In-app route to open when tapped, when there is somewhere to go.</summary>
    public string? ActionRoute { get; set; }

    /// <summary>
    /// What this notification is about, so a duplicate for the same event and recipient can be
    /// suppressed and so a stale one can be retracted when the subject changes.
    /// </summary>
    public string? SubjectType { get; set; }
    public string? SubjectId { get; set; }

    public DateTimeOffset OccurredUtc { get; set; }
    public DateTimeOffset? ReadUtc { get; set; }
    public DateTimeOffset? DeletedUtc { get; set; }

    public bool IsRead => ReadUtc is not null;
}
