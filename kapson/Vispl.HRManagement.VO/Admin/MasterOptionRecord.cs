namespace Vispl.HRManagement.VO.Admin;

public sealed class MasterOptionRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public required string Category { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public int SortOrder { get; set; }
    public string? ParentCode { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
