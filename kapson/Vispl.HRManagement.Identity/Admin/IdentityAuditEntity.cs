namespace Vispl.HRManagement.Identity.Admin;

public sealed class IdentityAuditEntity
{
    public long Id { get; set; }
    public string? TenantKey { get; set; }
    public string? ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string SubjectType { get; set; } = string.Empty;
    public string SubjectId { get; set; } = string.Empty;
    public string? DetailJson { get; set; }
    public DateTimeOffset OccurredUtc { get; set; }
}
