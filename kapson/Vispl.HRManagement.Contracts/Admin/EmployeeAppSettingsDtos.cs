namespace Vispl.HRManagement.Contracts.Admin;

/// <summary>
/// The HR portal's Employee App settings form. Every switch is enforced on the server as well as
/// hidden in the app: an old app build, or a request crafted by hand, gets the same answer.
/// </summary>
public sealed class EmployeeAppSettingsDto
{
    public bool AllowAppPunch { get; set; } = true;

    /// <summary>Saved now; takes effect when the app captures location (build plan phase D).</summary>
    public bool RequireLocationForPunch { get; set; }

    public bool AllowGatePassRequests { get; set; } = true;
    public bool AllowAttendanceCorrections { get; set; } = true;
    public bool ShowSalaryDetails { get; set; } = true;
    public bool ShowBankDetails { get; set; } = true;
    public bool ShowOnboarding { get; set; } = true;

    /// <summary>Saved now; the Update Profile flow that reads it is build plan phase E.</summary>
    public bool AllowProfileChangeRequests { get; set; } = true;

    /// <summary>The attendance % employees are measured against in Attendance Analytics (50–100).</summary>
    public int AttendanceTargetPercent { get; set; } = 85;

    public bool IsDefault { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
    public string? UpdatedBy { get; set; }
}
