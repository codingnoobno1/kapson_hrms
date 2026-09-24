namespace Vispl.HRManagement.DL.Admin;

public sealed class PolicyEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
    public string SectionsJson { get; set; } = "[]";
    public string Version { get; set; } = "1";
    public bool IsPublished { get; set; }
    public DateOnly UpdatedOn { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class PolicyAcknowledgementEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string EmployeeId { get; set; } = string.Empty;
    public long PolicyId { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateTimeOffset AcknowledgedUtc { get; set; }
}
