namespace Vispl.HRManagement.VO.Admin;

public sealed class TenantRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public required string DisplayName { get; set; }
    public string? PrimaryDomain { get; set; }
    public string TimeZoneId { get; set; } = "UTC";
    public int RetirementAgeYears { get; set; } = 58;
    public bool HrPortalEnabled { get; set; } = true;
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
