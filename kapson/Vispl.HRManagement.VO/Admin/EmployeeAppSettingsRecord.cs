namespace Vispl.HRManagement.VO.Admin;

/// <summary>
/// What the employee app lets this client's employees do, as stored. One row per tenant; a
/// tenant with no row runs on the defaults, which match what the app did before HR could
/// switch anything off.
/// </summary>
public sealed class EmployeeAppSettingsRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }

    public bool AllowAppPunch { get; set; } = true;
    public bool RequireLocationForPunch { get; set; }
    public bool AllowGatePassRequests { get; set; } = true;
    public bool AllowAttendanceCorrections { get; set; } = true;
    public bool ShowSalaryDetails { get; set; } = true;
    public bool ShowBankDetails { get; set; } = true;
    public bool ShowOnboarding { get; set; } = true;
    public bool AllowProfileChangeRequests { get; set; } = true;
    public int AttendanceTargetPercent { get; set; } = 85;

    public DateTimeOffset UpdatedUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
