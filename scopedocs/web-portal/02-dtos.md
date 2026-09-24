# Contracts

C# shapes for the KPIs in [01-kpis.md](01-kpis.md). These live in
`Vispl.HRManagement.Contracts/Mobile/Dashboard/` so the web portal and both apps share one
definition — the portal is simply the first client.

Conventions already established in the codebase and kept here: `record` for responses, `class`
with data annotations for queries, `DateOnly` for calendar dates, `DateTimeOffset` for instants,
`decimal` for days and money, and `CursorPage<T>` for anything that can grow.

---

## Scope and envelope

```csharp
/// <summary>
/// What the whole dashboard is computed for. Every widget takes the same scope, so the page
/// cannot show one card for May and another for today.
/// </summary>
public sealed class DashboardScopeQuery
{
    [StringLength(40)] public string? CompanyCode { get; set; }
    [StringLength(40)] public string? UnitCode { get; set; }

    /// <summary>The date the page is computed as of. Defaults to today.</summary>
    public DateOnly? AsOf { get; set; }
}

/// <summary>
/// Echoed back with every response. The client renders the as-of date it was actually given,
/// never the one it asked for, so a clamped or defaulted date is visible rather than silent.
/// </summary>
public sealed record DashboardScopeDto(
    string? CompanyCode,
    string? CompanyName,
    string? UnitCode,
    string? UnitName,
    DateOnly AsOf,
    bool IsHistorical,          // AsOf is not today — the UI must say so
    DateTimeOffset GeneratedUtc);
```

## A. Headline strip

```csharp
/// <summary>
/// One headline tile. Count and percentage travel together with the denominator that produced
/// the percentage, because "87.34%" is meaningless without knowing what it is 87% of.
/// </summary>
public sealed record HeadcountKpiDto(
    string Key,                 // total-employees, present-today, on-leave, absent, late-coming, pending-approval
    string Title,
    int Count,
    decimal? Percentage,
    int? Denominator,
    string DenominatorLabel,    // "of 482 employees" / "of 470 expected to work"
    string Tone,
    string? TargetRoute,
    string? Caption,            // "Across 3 Companies"
    string? UnavailableReason); // set when a module behind it is missing

public sealed record DashboardHeadlineDto(
    DashboardScopeDto Scope,
    IReadOnlyList<HeadcountKpiDto> Kpis);
```

## B. Policies

```csharp
public sealed record PolicySummaryDto(
    string Id,
    string Code,
    string Title,
    string Icon,
    string Excerpt,
    string Version,
    DateOnly UpdatedOn,
    bool IsAcknowledged,
    DateTimeOffset? AcknowledgedUtc,
    string? AcknowledgedVersion);   // null when read at an older version

public sealed record PolicySectionDto(string Heading, IReadOnlyList<string> Points);

public sealed record PolicyDetailDto(
    PolicySummaryDto Summary,
    IReadOnlyList<PolicySectionDto> Sections,
    bool RequiresAcknowledgement,
    IReadOnlyList<AttachmentMetadataDto> Attachments);

public sealed class AcknowledgePolicyRequest
{
    /// <summary>
    /// The version the reader actually saw. An acknowledgement that does not name a version is
    /// void the moment the policy is republished, so this is required rather than inferred.
    /// </summary>
    [Required, StringLength(20)] public string Version { get; set; } = string.Empty;
}

public sealed record PolicyComplianceDto(
    string PolicyId,
    string Title,
    int Applicable,
    int Acknowledged,
    decimal Rate);
```

## C. People moments

```csharp
public sealed record PersonMomentDto(
    string EmployeeId,
    string DisplayName,
    string? AvatarUrl,
    string? DepartmentName,
    string? Designation,
    DateOnly Date,
    int Years,                  // turning N, or N years of service
    string Window);             // Today | Belated | Upcoming

public sealed record PeopleMomentsDto(
    IReadOnlyList<PersonMomentDto> Birthdays,
    IReadOnlyList<PersonMomentDto> Anniversaries,
    int BelatedWindowDays,
    int UpcomingWindowDays);
```

## D. Late coming

```csharp
public sealed record LateArrivalRowDto(
    string EmployeeId,
    string DisplayName,
    string? DepartmentName,
    string? Designation,
    TimeOnly ExpectedTime,
    TimeOnly ArrivalTime,
    int DelayMinutes,           // derived: never accepted from a client
    int CumulativeLateMinutes,  // to the as-of date
    int MonthToDateLateMinutes,
    int WeekToDateLateMinutes,
    string? ShiftCode);         // null until the shift module lands

public sealed record LateArrivalReportDto(
    DashboardScopeDto Scope,
    CursorPage<LateArrivalRowDto> Rows,
    int TotalLateToday);
```

## E. Events

```csharp
public sealed record CompanyEventDto(
    string Id,
    string Name,
    DateOnly Date,
    string Type,                // Holiday | CompanyEvent | Observance
    string? Description,
    string? LocationCode,
    bool IsPaidHoliday);
```

## F. Leave analytics

```csharp
public sealed record LeaveBalanceCellDto(string PolicyCode, string PolicyName, decimal Available);

/// <summary>
/// One row of the employee-wise balance grid. <c>Total</c> is computed from
/// <c>Balances</c> here rather than sent alongside it, so the row cannot disagree with itself.
/// </summary>
public sealed record EmployeeLeaveBalanceRowDto(
    string EmployeeId,
    string DisplayName,
    string? DepartmentName,
    string? Designation,
    IReadOnlyList<LeaveBalanceCellDto> Balances)
{
    public decimal Total => Balances.Sum(balance => balance.Available);
}

public sealed record DepartmentLeaveBalanceRowDto(
    string DepartmentCode,
    string DepartmentName,
    int EmployeeCount,
    decimal UsedDays,
    decimal AvailableDays,
    decimal UtilisationPercent);

public sealed record LeaveCategorySliceDto(string PolicyCode, string PolicyName, decimal Days);

public sealed record DepartmentLeaveUsageDto(
    string DepartmentCode,
    string DepartmentName,
    decimal UsedDays,
    decimal AvailableDays);

public sealed record LeaveTrendPointDto(int Year, int Month, string MonthLabel, decimal Days);

public sealed record LeaveStatusSplitDto(int Approved, int Pending, int Rejected, int Withdrawn);

public sealed record YearEndDepartmentDto(
    string DepartmentCode,
    string DepartmentName,
    int PendingRequests,
    decimal TotalDays,
    decimal AverageDays);       // derived

public sealed record LeaveTypeUsageDto(
    string PolicyCode,
    string PolicyName,
    decimal Used,
    decimal Entitled,
    string Unit);               // "days" / "half-days"

public sealed record LeaveUsageByDateDto(
    DateOnly Date,
    IReadOnlyList<LeaveCategorySliceDto> ByCategory);

public sealed record LeaveAnalyticsDto(
    DashboardScopeDto Scope,
    IReadOnlyList<LeaveCategorySliceDto> CategoryDistribution,
    IReadOnlyList<DepartmentLeaveUsageDto> DepartmentUsage,
    IReadOnlyList<LeaveTrendPointDto> MonthlyTrend,
    LeaveStatusSplitDto StatusSplit);
```

## G. Movement and gate pass

```csharp
public sealed record MovementRowDto(
    string EmployeeId,
    string DisplayName,
    string? DepartmentName,
    string? Designation,
    string Kind,                // Personal | Official
    DateTimeOffset GoingOutUtc,
    DateTimeOffset ExpectedReturnUtc,
    DateTimeOffset? ActualReturnUtc,
    string? ReasonOrPurpose,
    bool IsOverdue);            // expected return passed, no return recorded

public sealed record MissPunchRowDto(
    string EmployeeId,
    string DisplayName,
    string? DepartmentName,
    string? Designation,
    DateOnly Date,
    TimeOnly? RecordedPunch,
    string MissingSide,         // CheckIn | CheckOut
    TimeOnly? ExpectedTime,
    string ExceptionId);        // links to the attendance exception already modelled

public sealed record GatePassRowDto(
    string Id,
    string EmployeeId,
    string DisplayName,
    string? DepartmentName,
    string? Designation,
    string PassType,            // DayPass | MultiDayPass
    DateOnly FromDate,
    DateOnly ToDate,
    string Purpose,
    StatusDto Status,
    IReadOnlyList<ActionAvailabilityDto> Actions);
```

## H. Attendance overview

```csharp
public sealed record EmployeeAttendanceRowDto(
    string EmployeeId,
    string DisplayName,
    string? DepartmentName,
    string? Designation,
    int PresentDays,
    int AbsentDays,
    int LeaveDays,
    int ExpectedDays,
    decimal AttendancePercent,
    string StatusBand,          // Excellent | Good | Fair | Poor
    string StatusTone);

/// <summary>
/// The thresholds behind <c>StatusBand</c>, returned with the data so the legend on screen and
/// the banding in the service can never drift apart.
/// </summary>
public sealed record AttendanceBandDto(string Band, decimal MinPercent, string Tone);

public sealed record AttendanceOverviewDto(
    DashboardScopeDto Scope,
    CursorPage<EmployeeAttendanceRowDto> Rows,
    IReadOnlyList<AttendanceBandDto> Bands);
```

## Composite

```csharp
/// <summary>
/// The whole dashboard in one round trip. Sections the caller may not see are absent rather
/// than empty, so the client renders what it was given instead of guessing at permissions.
/// </summary>
public sealed record HrDashboardDto(
    DashboardScopeDto Scope,
    DashboardHeadlineDto Headline,
    IReadOnlyList<PolicySummaryDto>? Policies,
    PeopleMomentsDto? PeopleMoments,
    IReadOnlyList<CompanyEventDto>? UpcomingEvents,
    LeaveAnalyticsDto? LeaveAnalytics,
    int? PendingGatePasses,
    int? MissPunchCount);
```

**A section the caller cannot see is `null`, not `[]`.** An empty list means "nothing today"; a
null means "not yours to see". Collapsing the two would make an empty birthday list and a
forbidden one indistinguishable.
