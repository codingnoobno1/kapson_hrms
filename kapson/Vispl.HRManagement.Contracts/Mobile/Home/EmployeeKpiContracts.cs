using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Mobile.Home;

/// <summary>
/// The identity card at the top of the employee Home screen.
///
/// <para><b>QrPayload</b> carries the employee number only. It identifies, it does not
/// authenticate: anyone can photograph it, so nothing that grants access may rely on it alone
/// until a rotating token is designed.</para>
/// </summary>
public sealed record EmployeeIdentityCardDto(
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string? JobTitle,
    string? DepartmentName,
    string? Phone,
    string? AvatarUrl,
    string QrPayload);

/// <summary>Home: the identity card and the KPI tiles beside it.</summary>
public sealed record EmployeeHomeSummaryDto(
    EmployeeIdentityCardDto Identity,
    IReadOnlyList<KpiDto> Kpis,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    DateTimeOffset GeneratedUtc);

/// <summary>
/// The four attendance tiles and the month they describe. The month is echoed back so the
/// screen cannot show a stepper and a grid that disagree about which month is on display.
/// </summary>
public sealed record AttendanceKpiSummaryDto(
    int Year,
    int Month,
    string MonthName,
    IReadOnlyList<KpiDto> Kpis,
    int WorkingDays,
    DateTimeOffset GeneratedUtc);

/// <summary>
/// The Home "Interview Schedule" card. Only interviewers see it: <see cref="IsInterviewer"/> is
/// true when the caller holds the interview permission or sits on a panel in the window, and the
/// app hides the card otherwise (employee-app-build-plan.md D3).
/// </summary>
public sealed record EmployeeInterviewScheduleDto(
    bool IsInterviewer,
    IReadOnlyList<Recruitment.InterviewListItemDto> Interviews,
    DateTimeOffset GeneratedUtc);

/// <summary>Leave balance tiles, one per policy the employee holds.</summary>
public sealed record LeaveKpiSummaryDto(
    int Year,
    IReadOnlyList<KpiDto> Kpis,
    DateTimeOffset GeneratedUtc);

public sealed class AttendanceKpiQuery
{
    /// <summary>Month as <c>yyyy-MM</c>. Defaults to the current month.</summary>
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Use yyyy-MM, for example 2026-09.")]
    public string? Month { get; set; }
}

public sealed class LeaveKpiQuery
{
    [Range(2000, 2100)]
    public int? Year { get; set; }
}

/// <summary>
/// Tiles the Home screen shows but no module can yet compute. Naming them here keeps the
/// contract stable: when the module lands the tile fills in and nothing else changes.
/// </summary>
public static class EmployeeKpiKeys
{
    public const string PresentDays = "present-days";
    public const string LeaveBalance = "leave-balance";

    /// <summary>
    /// Home tile 3 (mint): the caller's open leave requests, attendance corrections and gate
    /// passes. Took the Active Projects slot (employee-app-build-plan.md D2).
    /// </summary>
    public const string PendingRequests = "pending-requests";

    /// <summary>Home tile 4 (peach): gate passes requested this month. Took the Performance slot.</summary>
    public const string GatePasses = "gate-passes";

    public const string ActiveProjects = "active-projects";
    public const string Performance = "performance";

    public const string AbsentDays = "absent-days";
    public const string HalfDays = "half-days";
    public const string Holidays = "holidays";
}
