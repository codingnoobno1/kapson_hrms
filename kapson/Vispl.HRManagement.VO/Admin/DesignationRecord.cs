namespace Vispl.HRManagement.VO.Admin;

public sealed class DesignationRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public required string GradeCode { get; set; }
    public required string DepartmentCode { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
