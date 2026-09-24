using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Dashboard;

// ---- Scope options --------------------------------------------------------

public sealed record ScopeOptionDto(string Code, string Name, int EmployeeCount);

/// <summary>
/// What the filter bar may offer. Only companies and units the caller may actually read are
/// returned, so the filter cannot propose a scope that would then be refused.
/// </summary>
public sealed record DashboardScopeOptionsDto(
    IReadOnlyList<ScopeOptionDto> Companies,
    IReadOnlyList<ScopeOptionDto> Units,
    int TotalEmployees);

// ---- Late arrivals --------------------------------------------------------

public sealed record LateArrivalRowDto(
    string EmployeeId,
    string DisplayName,
    string? DepartmentCode,
    string? Designation,
    TimeOnly ExpectedTime,
    TimeOnly ArrivalTime,
    int DelayMinutes,
    int LateMinutesToDate,
    int LateMinutesThisMonth,
    int LateMinutesThisWeek,
    string? ShiftCode);

public sealed record LateArrivalReportDto(
    DashboardScopeDto Scope,
    CursorPage<LateArrivalRowDto> Rows,
    int TotalLate,
    TimeOnly AssumedStart,
    int GraceMinutes,
    /// <summary>
    /// Set while no shift module exists. The table is measured against one assumed start for
    /// everyone, and the screen has to say so rather than implying a roster was consulted.
    /// </summary>
    string? Caveat);

// ---- Missing punches ------------------------------------------------------

public sealed record MissPunchRowDto(
    string ExceptionId,
    string EmployeeId,
    string DisplayName,
    string? DepartmentCode,
    string? Designation,
    DateOnly Date,
    string MissingSide,
    string State,
    string StateTone,

    /// <summary>The punch that did happen (tenant local): the in-punch when check-out is missing, and the other way round.</summary>
    TimeOnly? PunchTime = null,

    /// <summary>When the missing punch was due by the shift: its end for a missing check-out, its start for a missing check-in.</summary>
    TimeOnly? ExpectedTime = null);

public sealed record MissPunchReportDto(
    DashboardScopeDto Scope,
    CursorPage<MissPunchRowDto> Rows,
    int OpenCount);

// ---- Leave balance grids --------------------------------------------------

public sealed record LeaveBalanceCellDto(string PolicyCode, decimal Available);

/// <summary>
/// One row of the employee-wise balance grid.
///
/// <c>Total</c> is computed from the cells rather than carried beside them. The prototype sent
/// both and they disagreed in four rows out of six.
/// </summary>
public sealed record EmployeeLeaveBalanceRowDto(
    string EmployeeId,
    string DisplayName,
    string? DepartmentCode,
    string? Designation,
    IReadOnlyList<LeaveBalanceCellDto> Balances)
{
    public decimal Total => Balances.Sum(balance => balance.Available);
}

public sealed record EmployeeLeaveBalanceGridDto(
    DashboardScopeDto Scope,
    int Year,
    IReadOnlyList<string> PolicyColumns,
    CursorPage<EmployeeLeaveBalanceRowDto> Rows);

public sealed record DepartmentLeaveBalanceRowDto(
    string DepartmentCode,
    int EmployeeCount,
    decimal UsedDays,
    decimal AvailableDays,
    decimal UtilisationPercent);

public sealed record DepartmentLeaveBalanceGridDto(
    DashboardScopeDto Scope,
    int Year,
    IReadOnlyList<DepartmentLeaveBalanceRowDto> Rows);

// ---- Leave by type --------------------------------------------------------

public sealed record LeaveTypeUsageDto(
    string PolicyCode,
    string PolicyName,
    decimal Used,
    decimal Entitled,
    string Unit);

public sealed record LeaveTypeUsageReportDto(
    DashboardScopeDto Scope,
    int Year,
    int Month,
    string MonthLabel,
    IReadOnlyList<LeaveTypeUsageDto> Rows);

/// <summary>One column of the stacked daily chart.</summary>
public sealed record LeaveUsageByDateDto(
    DateOnly Date,
    IReadOnlyList<LeaveCategorySliceDto> ByCategory)
{
    public decimal Total => ByCategory.Sum(slice => slice.Days);
}

/// <summary>
/// Daily leave usage for a month.
///
/// Only days the calendar actually works appear on the axis — the prototype's own chart skips
/// weekends, so the axis is working days rather than every date in the month.
/// </summary>
public sealed record LeaveUsageByDateReportDto(
    DashboardScopeDto Scope,
    int Year,
    int Month,
    string MonthLabel,
    IReadOnlyList<string> Categories,
    IReadOnlyList<LeaveUsageByDateDto> Days);

/// <summary>How many people have applied to one opening, and where they have reached.</summary>
public sealed record OpeningPipelineRowDto(
    string JobOpeningId,
    string Title,
    string? Department,
    string? Location,
    bool IsPublished,
    int TotalApplicants,
    int NewApplicants,
    int InProgress,
    int Hired,
    int Rejected,
    IReadOnlyList<StageCountDto> ByStage,
    DateTimeOffset? LatestApplicationUtc);

public sealed record StageCountDto(string Stage, int Count);

/// <summary>
/// The openings board: every vacancy with the people who have applied to it.
///
/// Openings with no applicants are kept. A vacancy nobody has applied to is the most useful
/// row on the board, and dropping it would hide exactly the problem worth seeing.
/// </summary>
public sealed record OpeningPipelineDto(
    DashboardScopeDto Scope,
    IReadOnlyList<OpeningPipelineRowDto> Openings,
    int TotalApplicants,
    int OpeningsWithNoApplicants);

/// <summary>One leave request waiting on a decision, named so HR knows whose it is.</summary>
public sealed record LeaveApprovalRowDto(
    string RequestId,
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string? DepartmentCode,
    string? Designation,
    string LeaveTypeCode,
    DateOnly From,
    DateOnly To,
    decimal Days,
    string Reason,
    DateTimeOffset SubmittedUtc,

    /// <summary>Whole days since it was raised. The oldest request is the one that hurts.</summary>
    int WaitingDays,

    /// <summary>True once the leave has started, or is about to, while still undecided.</summary>
    bool StartsWithinWindow,

    /// <summary>
    /// Set when this request belongs to the person reading the queue. Nobody decides their own
    /// leave, and saying so up front beats a refusal after the click.
    /// </summary>
    bool IsOwn,

    /// <summary>"Casual Leave" rather than "CL" — the approval card's header strip.</summary>
    string? LeaveTypeName = null,

    /// <summary>"Sales & Retail" rather than the department code.</summary>
    string? DepartmentName = null,

    /// <summary>The leave type's colour family (LeaveTones), set by HR per policy.</summary>
    string ToneKey = "blue",

    string Emoji = "🗓️");

/// <summary>
/// Every leave request in the client awaiting a decision — not just the reader's direct
/// reports, because the HR portal's queue is the client's queue.
///
/// The dashboard shows a Pending Approval count. A count with no way to see what it counts is
/// the complaint every HR tool earns, so this is the list behind that tile.
/// </summary>
public sealed record LeaveApprovalQueueDto(
    DashboardScopeDto Scope,
    IReadOnlyList<LeaveApprovalRowDto> Rows,
    int TotalPending,

    /// <summary>How many have already started, or start inside the warning window.</summary>
    int StartingSoon,

    /// <summary>The longest anything has waited, which is the number worth acting on.</summary>
    int LongestWaitDays);

// ---- Queries --------------------------------------------------------------

public class WorkforceReportQuery : DashboardScopeQuery
{
    [StringLength(200)] public string? Cursor { get; set; }
    [Range(1, 200)] public int PageSize { get; set; } = 25;
}

public sealed class MonthlyReportQuery : DashboardScopeQuery
{
    [RegularExpression(@"^\d{4}-(0[1-9]|1[0-2])$", ErrorMessage = "Use yyyy-MM, for example 2026-09.")]
    public string? Month { get; set; }
}

public sealed class LeaveBalanceGridQuery : WorkforceReportQuery
{
    [Range(2000, 2100)] public int? Year { get; set; }
}
