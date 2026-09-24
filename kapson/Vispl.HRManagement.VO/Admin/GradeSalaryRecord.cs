namespace Vispl.HRManagement.VO.Admin;

public sealed class GradeSalaryRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public required string GradeCode { get; set; }
    public decimal BasicMonthly { get; set; }
    public required string ComponentsJson { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
