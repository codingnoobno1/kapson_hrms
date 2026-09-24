using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Dashboard;

/// <summary>
/// What the whole dashboard is computed for. Every widget takes the same scope, so the page
/// cannot show one card for May and another for today.
/// </summary>
public class DashboardScopeQuery
{
    [StringLength(40)] public string? CompanyCode { get; set; }
    [StringLength(40)] public string? UnitCode { get; set; }

    /// <summary>The date the page is computed as of. Defaults to today.</summary>
    public DateOnly? AsOf { get; set; }
}

/// <summary>
/// Echoed back with every response. The client renders the as-of date it was actually given
/// rather than the one it asked for, so a defaulted or clamped date is visible rather than
/// silent — the prototype showed a header date and a filter date four months apart.
/// </summary>
public sealed record DashboardScopeDto(
    string? CompanyCode,
    string? UnitCode,
    DateOnly AsOf,
    bool IsHistorical,
    int PopulationCount,
    DateTimeOffset GeneratedUtc);

/// <summary>
/// One headline tile. The count, the percentage and the denominator travel together, because
/// "87.34%" means nothing without knowing what it is a percentage of.
/// </summary>
public sealed record HeadcountKpiDto(
    string Key,
    string Title,
    int Count,
    decimal? Percentage,
    int? Denominator,
    string DenominatorLabel,
    string Tone,
    string? TargetRoute,
    string? Caption,
    string? UnavailableReason = null);

public sealed record DashboardHeadlineDto(
    DashboardScopeDto Scope,
    IReadOnlyList<HeadcountKpiDto> Kpis);

// ---- People moments -------------------------------------------------------

public sealed record PersonMomentDto(
    string EmployeeId,
    string DisplayName,
    string? AvatarUrl,
    string? DepartmentCode,
    string? Designation,
    DateOnly Date,
    int Years,
    string Window);

public sealed record PeopleMomentsDto(
    DashboardScopeDto Scope,
    IReadOnlyList<PersonMomentDto> Birthdays,
    IReadOnlyList<PersonMomentDto> Anniversaries,
    int BelatedWindowDays,
    int UpcomingWindowDays);

public sealed class PeopleMomentsQuery : DashboardScopeQuery
{
    [Range(0, 60)] public int BelatedDays { get; set; } = 7;
    [Range(0, 60)] public int UpcomingDays { get; set; } = 7;
}

// ---- Leave analytics ------------------------------------------------------

public sealed record LeaveCategorySliceDto(string PolicyCode, string PolicyName, decimal Days);

public sealed record LeaveTrendPointDto(int Year, int Month, string MonthLabel, decimal Days);

public sealed record LeaveStatusSplitDto(int Approved, int Pending, int Rejected, int Withdrawn);

public sealed record LeaveAnalyticsDto(
    DashboardScopeDto Scope,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<LeaveCategorySliceDto> CategoryDistribution,
    IReadOnlyList<LeaveTrendPointDto> MonthlyTrend,
    LeaveStatusSplitDto StatusSplit);

// ---- Attendance overview --------------------------------------------------

public sealed record EmployeeAttendanceRowDto(
    string EmployeeId,
    string DisplayName,
    string? DepartmentCode,
    string? Designation,
    int PresentDays,
    int AbsentDays,
    decimal LeaveDays,
    int ExpectedDays,
    decimal AttendancePercent,
    string StatusBand,
    string StatusTone);

/// <summary>
/// The thresholds behind <c>StatusBand</c>, returned with the rows so the legend on screen and
/// the banding in the service cannot drift apart. The prototype labelled 90% "Fair" and 75%
/// "Good" on the same table.
/// </summary>
public sealed record AttendanceBandDto(string Band, decimal MinPercent, string Tone);

public sealed record AttendanceOverviewDto(
    DashboardScopeDto Scope,
    int Year,
    int Month,
    CursorPage<EmployeeAttendanceRowDto> Rows,
    IReadOnlyList<AttendanceBandDto> Bands);

public sealed class AttendanceOverviewQuery : DashboardScopeQuery
{
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Use yyyy-MM, for example 2026-09.")]
    public string? Month { get; set; }

    [StringLength(200)] public string? Cursor { get; set; }
    [Range(1, 200)] public int PageSize { get; set; } = 25;
}

// ---- Composite ------------------------------------------------------------

/// <summary>
/// The whole dashboard in one round trip.
///
/// A section the caller may not see is <c>null</c>, never an empty list: an empty birthday list
/// means nobody has a birthday, while null means it is not theirs to see, and collapsing the two
/// would make a permission failure look like a quiet day.
/// </summary>
public sealed record HrDashboardDto(
    DashboardScopeDto Scope,
    DashboardHeadlineDto Headline,
    PeopleMomentsDto? PeopleMoments,
    LeaveAnalyticsDto? LeaveAnalytics,
    IReadOnlyList<CompanyEventDto>? UpcomingEvents,
    IReadOnlyList<PolicySummaryDto>? Policies);

public sealed record CompanyEventDto(
    string Id,
    string Name,
    DateOnly Date,
    string Type,
    string? Description,
    string? LocationCode);

public static class DashboardKpiKeys
{
    public const string TotalEmployees = "total-employees";
    public const string PresentToday = "present-today";
    public const string OnLeave = "on-leave";
    public const string Absent = "absent";
    public const string LateComing = "late-coming";
    public const string PendingApproval = "pending-approval";
}

// ---- KPI drill-down --------------------------------------------------------

/// <summary>
/// One person behind a headline tile. <c>Detail</c> is whatever makes that particular tile's
/// membership legible — an arrival time for Present Today, a leave type for On Leave, a request's
/// dates for Pending Approval — rather than a column every tile has to force data into.
/// </summary>
public sealed record DashboardKpiMemberDto(
    string EmployeeId,
    string DisplayName,
    string? DepartmentCode,
    string? Designation,
    string Detail,
    string Tone);

/// <summary>
/// The people a single headline tile counts, resolved with the same scope and the same rules the
/// tile itself used — so the row count on this page can never disagree with the number on the
/// card that links to it.
/// </summary>
public sealed record DashboardKpiMembersDto(
    DashboardScopeDto Scope,
    string Key,
    string Title,
    IReadOnlyList<DashboardKpiMemberDto> Rows);
