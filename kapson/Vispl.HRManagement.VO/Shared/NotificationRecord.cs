namespace Vispl.HRManagement.VO.Shared;

public sealed record NotificationRecord(
    long Id,
    string TenantKey,
    string RecipientId,
    string Category,
    string Title,
    string Body,
    string Priority,
    string? ActionRoute,
    string? SubjectType,
    string? SubjectId,
    DateTimeOffset OccurredUtc,
    DateTimeOffset? ReadUtc)
{
    public bool IsRead => ReadUtc is not null;
}
