namespace Vispl.HRManagement.Contracts.Admin;

/// <summary>
/// Filters shared by the Employee tab and the Employee Report pages. No reference layout exists
/// for these destinations (scopedocs/web-portal/10 §1, "named only"); the shapes follow the data
/// the platform actually holds.
/// </summary>
public sealed class EmployeeReportQuery
{
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }

    /// <summary>"Active", "Inactive" or empty for everyone.</summary>
    public string? Status { get; set; }

    public string? Search { get; set; }

    /// <summary>Monthly attendance report: one employee only (the EMPLOYEE filter).</summary>
    public string? EmployeeId { get; set; }

    /// <summary>Monthly attendance report: one company (More Filters).</summary>
    public string? CompanyCode { get; set; }

    /// <summary>Month-based reports only.</summary>
    public int Month { get; set; } = DateTime.Today.Month;

    public int Year { get; set; } = DateTime.Today.Year;
}

public sealed record EmployeeDirectoryRowDto(
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string? JobTitle,
    string DivisionCode,
    string DivisionName,
    string DepartmentCode,
    string DepartmentName,
    string LocationName,
    string? GradeCode,
    string? GradeName,
    DateOnly JoinedOn,
    string EmploymentType,
    string EmploymentStatus,
    bool IsActive,
    string? Email,
    string? Phone,
    string? ManagerId,
    string? ManagerName,
    int DirectReports);

// ---- Organisation Hierarchy ---------------------------------------------

public sealed record OrgHierarchyNodeDto(
    EmployeeDirectoryRowDto Employee,
    IReadOnlyList<OrgHierarchyNodeDto> Reports,
    int TeamSize);

public sealed record OrgDepartmentGroupDto(
    string DepartmentCode,
    string DepartmentName,
    IReadOnlyList<EmployeeDirectoryRowDto> Employees);

public sealed record OrgDivisionGroupDto(
    string DivisionCode,
    string DivisionName,
    int EmployeeCount,
    IReadOnlyList<OrgDepartmentGroupDto> Departments);

public sealed record OrgHierarchyDto(
    int TotalEmployees,
    int ManagerCount,
    int WithoutManager,
    int MaxDepth,
    IReadOnlyList<OrgHierarchyNodeDto> ReportingRoots,
    IReadOnlyList<OrgDivisionGroupDto> Divisions);

// ---- Employee Monthly Attendance Report -----------------------------------

public static class AttendanceDayCodes
{
    public const string Present = "P";
    public const string Partial = "½";
    public const string Absent = "A";
    public const string Leave = "L";
    public const string Holiday = "H";
    public const string WeeklyOff = "W";
    public const string Unrecorded = "-";

    /// <summary>A day before joining or after today: not part of the employee's month yet.</summary>
    public const string NotApplicable = "";
}

public sealed record AttendanceReportRowDto(
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string DivisionName,
    string DepartmentName,
    int Present,
    int Partial,
    int Absent,
    int Leave,
    int Holiday,
    int WeeklyOff,
    int Unrecorded,
    decimal WorkedHours,
    IReadOnlyList<string> Days,
    string? CompanyName = null,
    string? Designation = null,

    /// <summary>"09:00–18:30 (Flexible)" — the shift the employee was scheduled on this month.</summary>
    string? ShiftLabel = null,

    /// <summary>One per day of the month: the Detailed Attendance Records cells.</summary>
    IReadOnlyList<AttendanceReportCellDto>? Cells = null);

/// <summary>
/// One day cell on the Detailed Attendance Records grid. <see cref="Code"/> is P, A, W, H, a leave
/// policy code (SL, CL, AL…), HDF / HDS for a half day worked in the first / second half of the shift,
/// "-" for a working day with no record, or empty before joining / after today.
/// <see cref="Tone"/> is present, absent, leave, half-day, weekly-off, holiday, unrecorded or na.
/// </summary>
public sealed record AttendanceReportCellDto(
    DateOnly Date,
    string Code,
    string Tone,
    TimeOnly? CheckIn,
    TimeOnly? CheckOut,
    bool IsEditable);

/// <summary>The four report tiles. Attendance rate = (present + ½ half days) ÷ (present + half days + absent).</summary>
public sealed record AttendanceReportSummaryDto(int PresentDays, int AbsentDays, int LeaveDays, decimal AttendanceRate);

/// <summary>The P · A · L · W dots on one calendar day, across everyone in the report.</summary>
public sealed record AttendanceDailyTotalDto(DateOnly Date, int Present, int Absent, int Leave, int WeeklyOff);

public sealed record MonthlyAttendanceReportDto(
    int Month,
    int Year,
    string MonthLabel,
    int DaysInMonth,
    IReadOnlyList<string> WeekdayInitials,
    IReadOnlyList<AttendanceReportRowDto> Rows,
    AttendanceReportSummaryDto? Summary = null,
    IReadOnlyList<AttendanceDailyTotalDto>? DailyTotals = null,

    /// <summary>True when the reader holds ATTENDANCE.MANAGE, so the cells show their edit icon.</summary>
    bool CanEdit = false);

// ---- Employees Salary Register -------------------------------------------

public sealed record SalaryRegisterRowDto(
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string DivisionName,
    string DepartmentName,
    string Designation,
    string GradeCode,
    int PayableDays,
    int DaysInMonth,
    PayrollAmountsDto Amounts,
    string Status,
    DateOnly? PaidOn,
    string? PaymentMode);

public sealed record SalaryRegisterDto(
    int Month,
    int Year,
    string MonthLabel,
    bool RunExists,
    bool IsApproved,
    DateTimeOffset? ApprovedUtc,
    IReadOnlyList<SalaryRegisterRowDto> Rows,
    PayrollAmountsDto Totals);
