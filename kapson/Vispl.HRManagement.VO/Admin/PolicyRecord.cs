namespace Vispl.HRManagement.VO.Admin;

public sealed class PolicyRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public required string Code { get; set; }
    public required string Title { get; set; }
    public required string Icon { get; set; }
    public required string Excerpt { get; set; }

    /// <summary>Serialized <c>PolicySectionDto[]</c> — a policy's sections are read as one document,
    /// never queried section-by-section, so a JSON column costs nothing and avoids a child table.</summary>
    public required string SectionsJson { get; set; }

    public required string Version { get; set; }
    public bool IsPublished { get; set; }
    public DateOnly UpdatedOn { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

public sealed class PolicyAcknowledgementRecord
{
    public required string TenantKey { get; set; }
    public required string EmployeeId { get; set; }
    public required long PolicyId { get; set; }
    public required string Version { get; set; }
    public DateTimeOffset AcknowledgedUtc { get; set; }
}
