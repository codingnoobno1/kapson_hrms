namespace Vispl.HRManagement.DL.Admin;

public sealed class GradeSalaryEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string GradeCode { get; set; } = string.Empty;
    public decimal BasicMonthly { get; set; }
    public string ComponentsJson { get; set; } = "[]";
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
