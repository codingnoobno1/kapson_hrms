using System.Globalization;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Dashboard;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Recruitment;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Employee;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// The dashboard's drill-down tables.
///
/// Like the summary, everything is derived at read time and computed over one resolved
/// population. Nothing here accepts a total or a delay from a caller: the prototype's own
/// numbers disagreed with its own columns in several rows, and the only durable fix is to stop
/// storing what can be calculated.
/// </summary>
public sealed class WorkforceReportService(
    IEmployeeRepository employees,
    IAttendanceRepository attendance,
    ILeaveRepository leave,
    IShiftScheduleService schedule,
    Vispl.HRManagement.DL.ITF.Recruitment.IJobOpeningRepository openings,
    Vispl.HRManagement.DL.ITF.Recruitment.IApplicationRepository applications,
    Vispl.HRManagement.BM.ITF.Shared.ITenantClock clock,
    Vispl.HRManagement.DL.ITF.Admin.IMasterOptionRepository masters) : IWorkforceReportService
{
    private const string ViewPermission = "DASHBOARD.VIEW";
    private const string AttendanceReportPermission = "ATTENDANCE.REPORT";
    private const string LeaveReportPermission = "LEAVE.REPORT";

    /// <summary>
    /// Said only when nobody in the population has a shift assigned, so the numbers rest on the
    /// tenant default rather than on a roster. Once anyone is assigned, the caveat disappears.
    /// </summary>
    private const string FallbackCaveat =
        "Nobody in this scope has a shift assigned, so times are measured against the default shift.";

    // ---- Scope options ----------------------------------------------------

    public async Task<DashboardScopeOptionsDto> GetScopeOptionsAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        Require(caller, ViewPermission);

        // The picker counts the same people the dashboard will: a company whose option says 40
        // and whose tiles then say 39 is the drift this codebase keeps closing.
        var today = await clock.TodayAsync(caller.TenantKey, ct);

        var (companies, units) = await employees.GetScopeOptionsAsync(caller.TenantKey, today, ct);
        var total = await employees.CountAsync(
            caller.TenantKey,
            new EmployeeQueryFilter(null, null, null, null, null,
                ActiveOnly: true, JoinedOnOrBefore: today),
            ct);

        return new DashboardScopeOptionsDto(
            companies.Select(item => new ScopeOptionDto(item.Code, item.Name, item.EmployeeCount)).ToList(),
            units.Select(item => new ScopeOptionDto(item.Code, item.Name, item.EmployeeCount)).ToList(),
            total);
    }

    // ---- Late arrivals ----------------------------------------------------

    public async Task<LateArrivalReportDto> GetLateArrivalsAsync(
        MobileCallerContext caller, WorkforceReportQuery query, CancellationToken ct)
    {
        Require(caller, AttendanceReportPermission);

        var scope = await ResolveScopeAsync(caller, query, ct);
        var staff = await LoadStaffAsync(scope, ct);

        var today = await attendance.GetFirstInPunchAsync(
            scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct);

        // Cumulative columns need every day's first punch, so the range is fetched once rather
        // than once per employee per column.
        var yearStart = new DateOnly(scope.AsOf.Year, 1, 1);
        var history = await attendance.GetFirstInPunchRangeAsync(
            scope.TenantKey, yearStart, scope.AsOf, scope.EmployeeIds, ct);

        // One resolution for the whole year and population; every delay below is measured
        // against the shift that person actually worked that day.
        var shifts = await schedule.ResolveAsync(
            scope.TenantKey, scope.EmployeeIds, yearStart, scope.AsOf, ct);

        var monthStart = new DateOnly(scope.AsOf.Year, scope.AsOf.Month, 1);
        var weekStart = scope.AsOf.AddDays(-(int)scope.AsOf.DayOfWeek);

        var toDate = new Dictionary<long, int>();
        var thisMonth = new Dictionary<long, int>();
        var thisWeek = new Dictionary<long, int>();

        foreach (var (employeeId, workDate, firstIn) in history)
        {
            var delay = shifts.DelayMinutes(employeeId, workDate, firstIn) ?? 0;
            if (delay <= 0)
            {
                continue;
            }

            toDate[employeeId] = toDate.GetValueOrDefault(employeeId) + delay;

            if (workDate >= monthStart)
            {
                thisMonth[employeeId] = thisMonth.GetValueOrDefault(employeeId) + delay;
            }

            if (workDate >= weekStart)
            {
                thisWeek[employeeId] = thisWeek.GetValueOrDefault(employeeId) + delay;
            }
        }

        var rows = new List<LateArrivalRowDto>();

        foreach (var (employeeId, firstIn) in today)
        {
            var delay = shifts.DelayMinutes(employeeId, scope.AsOf, firstIn) ?? 0;
            if (delay <= 0 || !staff.TryGetValue(employeeId, out var person))
            {
                continue;
            }

            var expected = shifts.For(employeeId, scope.AsOf);

            rows.Add(new LateArrivalRowDto(
                employeeId.ToString(CultureInfo.InvariantCulture),
                DisplayName(person),
                person.DepartmentCode,
                person.JobTitle,
                expected?.StartTime ?? default,
                TimeOnly.FromDateTime(firstIn.UtcDateTime),
                delay,
                toDate.GetValueOrDefault(employeeId),
                thisMonth.GetValueOrDefault(employeeId),
                thisWeek.GetValueOrDefault(employeeId),
                expected?.ShiftCode));
        }

        var ordered = rows.OrderByDescending(row => row.DelayMinutes).ToList();

        var reference = shifts.Fallback;

        return new LateArrivalReportDto(
            scope.Dto,
            Page(ordered, query.Cursor, query.PageSize),
            ordered.Count,
            reference?.StartTime ?? default,
            reference?.GraceMinutes ?? 0,
            shifts.UsesFallbackOnly ? FallbackCaveat : null);
    }

    // ---- Missing punches --------------------------------------------------

    public async Task<MissPunchReportDto> GetMissingPunchesAsync(
        MobileCallerContext caller, WorkforceReportQuery query, CancellationToken ct)
    {
        Require(caller, AttendanceReportPermission);

        var scope = await ResolveScopeAsync(caller, query, ct);
        var staff = await LoadStaffAsync(scope, ct);

        // Reads the exceptions the attendance engine already raises rather than re-deriving
        // them, so the dashboard and the employee's own screen agree about what is missing.
        var from = scope.AsOf.AddDays(-30);

        var exceptions = await attendance.QueryExceptionsForAsync(
            scope.TenantKey, scope.EmployeeIds, from, scope.AsOf, "MissingPunch", null, ct);

        // The exception does not record which half of the day is missing, so it is derived from
        // the punches themselves rather than assumed.
        var counts = await attendance.GetPunchCountsAsync(
            scope.TenantKey, from, scope.AsOf, scope.EmployeeIds, ct);

        var parity = counts.ToDictionary(
            item => (item.EmployeeId, item.WorkDate),
            item => item.InCount - item.OutCount);

        // The punch that did happen, and when the missing one was due, for W08's Punch Time and
        // Expected Time columns.
        var dayPunches = (await attendance.GetPunchesForAsync(scope.TenantKey, scope.EmployeeIds, from, scope.AsOf, ct))
            .GroupBy(item => (item.EmployeeId, item.WorkDate))
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.OccurredUtc).ToList());
        var shifts = await schedule.ResolveAsync(scope.TenantKey, scope.EmployeeIds, from, scope.AsOf, ct);
        var zone = await clock.ResolveZoneAsync(scope.TenantKey, ct);

        var rows = exceptions
            .Where(item => staff.ContainsKey(item.EmployeeId))
            .Select(item =>
            {
                var person = staff[item.EmployeeId];
                var difference = parity.GetValueOrDefault((item.EmployeeId, item.WorkDate));
                var side = difference switch
                {
                    > 0 => "CheckOut",   // clocked in and never out
                    < 0 => "CheckIn",    // clocked out with no matching in
                    _ => "Unknown"
                };

                var punches = dayPunches.GetValueOrDefault((item.EmployeeId, item.WorkDate)) ?? [];
                var happened = side == "CheckIn"
                    ? punches.LastOrDefault(punch => string.Equals(punch.Type, "Out", StringComparison.OrdinalIgnoreCase))
                    : punches.FirstOrDefault(punch => string.Equals(punch.Type, "In", StringComparison.OrdinalIgnoreCase));
                var expected = shifts.For(item.EmployeeId, item.WorkDate);
                var start = expected?.StartTime ?? shifts.Fallback?.StartTime;
                var end = expected?.EndTime ?? shifts.Fallback?.EndTime;

                return new MissPunchRowDto(
                    item.Id.ToString(CultureInfo.InvariantCulture),
                    item.EmployeeId.ToString(CultureInfo.InvariantCulture),
                    DisplayName(person),
                    person.DepartmentCode,
                    person.JobTitle,
                    item.WorkDate,
                    side,
                    item.State,
                    item.State switch
                    {
                        "Open" => "warning",
                        "Confirmed" => "info",
                        "Resolved" => "success",
                        _ => "muted"
                    },
                    happened is null ? null : TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(happened.OccurredUtc, zone).DateTime),
                    side == "CheckIn" ? start : end);
            })
            .ToList();

        return new MissPunchReportDto(
            scope.Dto,
            Page(rows, query.Cursor, query.PageSize),
            rows.Count(row => row.State == "Open"));
    }

    // ---- Leave balance grids ----------------------------------------------

    public async Task<EmployeeLeaveBalanceGridDto> GetEmployeeLeaveBalancesAsync(
        MobileCallerContext caller, LeaveBalanceGridQuery query, CancellationToken ct)
    {
        Require(caller, LeaveReportPermission);

        var scope = await ResolveScopeAsync(caller, query, ct);
        var year = query.Year ?? scope.AsOf.Year;
        var staff = await LoadStaffAsync(scope, ct);

        var policies = await leave.GetPoliciesAsync(scope.TenantKey, true, ct);

        // One column per active policy, not a hard-coded CL/EL/SL: a tenant may run any number.
        var columns = policies.Select(policy => policy.Code).ToList();

        var rows = new List<EmployeeLeaveBalanceRowDto>();

        foreach (var (employeeId, person) in staff)
        {
            var balances = await leave.GetBalancesAsync(scope.TenantKey, employeeId, year, ct);
            var consumption = await leave.GetConsumptionAsync(scope.TenantKey, employeeId, year, ct);

            var cells = policies.Select(policy =>
            {
                var balance = balances.FirstOrDefault(item => item.PolicyId == policy.Id);
                var used = consumption.FirstOrDefault(item => item.PolicyId == policy.Id);

                var granted = (balance?.Accrued ?? 0m) + (balance?.CarriedForward ?? 0m) +
                              (balance?.AdjustmentDays ?? 0m);

                return new LeaveBalanceCellDto(
                    policy.Code, granted - (used?.UsedDays ?? 0m) - (used?.PendingDays ?? 0m));
            }).ToList();

            rows.Add(new EmployeeLeaveBalanceRowDto(
                employeeId.ToString(CultureInfo.InvariantCulture),
                DisplayName(person),
                person.DepartmentCode,
                person.JobTitle,
                cells));
        }

        var ordered = rows.OrderByDescending(row => row.Total).ToList();

        return new EmployeeLeaveBalanceGridDto(
            scope.Dto, year, columns, Page(ordered, query.Cursor, query.PageSize));
    }

    public async Task<DepartmentLeaveBalanceGridDto> GetDepartmentLeaveBalancesAsync(
        MobileCallerContext caller, LeaveBalanceGridQuery query, CancellationToken ct)
    {
        Require(caller, LeaveReportPermission);

        var scope = await ResolveScopeAsync(caller, query, ct);
        var year = query.Year ?? scope.AsOf.Year;
        var staff = await LoadStaffAsync(scope, ct);

        var from = new DateOnly(year, 1, 1);
        var to = new DateOnly(year, 12, 31);
        var usedByEmployee = await leave.SumDaysByEmployeeAsync(
            scope.TenantKey, from, to, scope.EmployeeIds, ct);

        var availableByEmployee = new Dictionary<long, decimal>();

        foreach (var employeeId in scope.EmployeeIds)
        {
            var balances = await leave.GetBalancesAsync(scope.TenantKey, employeeId, year, ct);
            availableByEmployee[employeeId] = balances.Sum(
                item => item.Accrued + item.CarriedForward + item.AdjustmentDays);
        }

        var rows = staff.Values
            .GroupBy(person => person.DepartmentCode ?? "Unassigned")
            .Select(group =>
            {
                var ids = group.Select(person => person.Id).ToList();
                var used = ids.Sum(id => usedByEmployee.GetValueOrDefault(id));
                var granted = ids.Sum(id => availableByEmployee.GetValueOrDefault(id));
                var available = granted - used;

                // Utilisation is used over granted, so a department that has been granted
                // nothing reads as zero rather than as fully utilised.
                var utilisation = granted <= 0 ? 0m : Math.Round(used * 100m / granted, 1);

                return new DepartmentLeaveBalanceRowDto(
                    group.Key, ids.Count, used, available, utilisation);
            })
            .OrderByDescending(row => row.EmployeeCount)
            .ToList();

        return new DepartmentLeaveBalanceGridDto(scope.Dto, year, rows);
    }

    // ---- Leave by type ----------------------------------------------------

    public async Task<LeaveTypeUsageReportDto> GetLeaveByTypeAsync(
        MobileCallerContext caller, MonthlyReportQuery query, CancellationToken ct)
    {
        Require(caller, LeaveReportPermission);

        var scope = await ResolveScopeAsync(caller, query, ct);
        var (year, month) = ResolveMonth(query.Month, scope.AsOf);

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var policies = await leave.GetPoliciesAsync(scope.TenantKey, true, ct);
        var used = await leave.SumDaysByPolicyAsync(scope.TenantKey, from, to, scope.EmployeeIds, ct);

        // Entitlement across the population, so the bar reads "used of what this group holds"
        // rather than of one person's allowance.
        var rows = policies.Select(policy => new LeaveTypeUsageDto(
            policy.Code,
            policy.Name,
            used.GetValueOrDefault(policy.Id),
            policy.AnnualEntitlement * scope.EmployeeIds.Count,
            policy.Unit.ToLowerInvariant())).ToList();

        return new LeaveTypeUsageReportDto(
            scope.Dto,
            year,
            month,
            CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month),
            rows);
    }

    public async Task<LeaveUsageByDateReportDto> GetLeaveByDateAsync(
        MobileCallerContext caller, MonthlyReportQuery query, CancellationToken ct)
    {
        Require(caller, LeaveReportPermission);

        var scope = await ResolveScopeAsync(caller, query, ct);
        var (year, month) = ResolveMonth(query.Month, scope.AsOf);

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var policies = await leave.GetPoliciesAsync(scope.TenantKey, true, ct);
        var byName = policies.ToDictionary(policy => policy.Id);

        var rows = await leave.SumDaysByDateAndPolicyAsync(
            scope.TenantKey, from, to, scope.EmployeeIds, ct);

        var holidays = await leave.GetHolidaysAsync(scope.TenantKey, from, to, null, ct);
        var holidayDates = holidays.Select(item => item.Date).ToHashSet();

        // The axis is working days. A month of weekends on the axis would be mostly empty
        // columns, and the prototype's own chart skips them.
        var shifts = await schedule.ResolveAsync(scope.TenantKey, scope.EmployeeIds, from, to, ct);

        var byDate = rows
            .GroupBy(item => item.Date)
            .ToDictionary(group => group.Key, group => group.ToList());

        var days = new List<LeaveUsageByDateDto>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var anyoneWorks = scope.EmployeeIds.Any(
                employeeId => shifts.For(employeeId, date)?.IsWorkingDay == true);

            var taken = byDate.GetValueOrDefault(date) ?? [];

            // A day nobody works and nobody took leave on is not worth a column. A day somebody
            // took leave on always is, even if it turns out to be a holiday.
            if (!anyoneWorks && taken.Count == 0)
            {
                continue;
            }

            if (holidayDates.Contains(date) && taken.Count == 0)
            {
                continue;
            }

            days.Add(new LeaveUsageByDateDto(
                date,
                policies.Select(policy => new LeaveCategorySliceDto(
                    policy.Code,
                    policy.Name,
                    taken.FirstOrDefault(item => item.PolicyId == policy.Id).Days)).ToList()));
        }

        // Only policies actually taken this month reach the chart. A legend listing six types
        // when three were used makes the chart harder to read, not more complete.
        var usedPolicies = policies
            .Where(policy => days.Any(day =>
                day.ByCategory.Any(category => category.PolicyCode == policy.Code && category.Days > 0)))
            .ToList();

        var trimmed = days
            .Select(day => new LeaveUsageByDateDto(
                day.Date,
                day.ByCategory
                    .Where(category => usedPolicies.Any(policy => policy.Code == category.PolicyCode))
                    .ToList()))
            .ToList();

        return new LeaveUsageByDateReportDto(
            scope.Dto,
            year,
            month,
            CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month),
            usedPolicies.Select(policy => policy.Name).ToList(),
            trimmed);
    }

    // ---- Openings and who applied to them ---------------------------------

    /// <summary>Stages that mean the application is still being worked.</summary>
    private static readonly string[] ActiveStages =
        [
            ApplicationStages.Applied,
            ApplicationStages.Screening,
            ApplicationStages.Shortlisted,
            ApplicationStages.Interviewing,
            ApplicationStages.Offered,
            ApplicationStages.OfferAccepted,
            ApplicationStages.Onboarding
        ];

    public async Task<OpeningPipelineDto> GetOpeningPipelineAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct)
    {
        Require(caller, "RECRUITMENT.VIEW");

        var scope = await ResolveScopeAsync(caller, query, ct);

        var vacancies = await openings.GetAllAsync(scope.TenantKey, ct);
        var counts = await applications.CountByOpeningAndStageAsync(scope.TenantKey, ct);

        var byOpening = counts
            .GroupBy(item => item.JobOpeningId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rows = new List<OpeningPipelineRowDto>();

        foreach (var vacancy in vacancies)
        {
            var stages = byOpening.GetValueOrDefault(vacancy.Id) ?? [];

            rows.Add(new OpeningPipelineRowDto(
                vacancy.Id.ToString(CultureInfo.InvariantCulture),
                vacancy.Title,
                vacancy.Department,
                vacancy.Location,
                vacancy.IsPublished,
                stages.Sum(item => item.Count),
                stages.Where(item => item.Stage == ApplicationStages.Applied).Sum(item => item.Count),
                stages.Where(item => ActiveStages.Contains(item.Stage) && item.Stage != ApplicationStages.Applied)
                      .Sum(item => item.Count),
                stages.Where(item => item.Stage == ApplicationStages.Hired).Sum(item => item.Count),
                stages.Where(item => item.Stage is ApplicationStages.Rejected or ApplicationStages.Withdrawn or ApplicationStages.OfferDeclined)
                      .Sum(item => item.Count),
                stages.OrderBy(item => item.Stage)
                      .Select(item => new StageCountDto(item.Stage, item.Count))
                      .ToList(),
                stages.Count == 0 ? null : stages.Max(item => item.LatestUtc)));
        }

        // Newest activity first, then the vacancies nobody has applied to — those are the ones
        // that need attention, so they sit together at the end rather than scattered.
        var ordered = rows
            .OrderByDescending(row => row.TotalApplicants > 0)
            .ThenByDescending(row => row.LatestApplicationUtc)
            .ThenBy(row => row.Title)
            .ToList();

        return new OpeningPipelineDto(
            scope.Dto,
            ordered,
            ordered.Sum(row => row.TotalApplicants),
            ordered.Count(row => row.TotalApplicants == 0 && row.IsPublished));
    }

    // ---- Leave approval queue ---------------------------------------------

    /// <summary>
    /// How close a start date has to be before an undecided request is called urgent. Leave
    /// that has already begun is the worst case and is counted here too.
    /// </summary>
    private const int StartingSoonDays = 3;

    public async Task<LeaveApprovalQueueDto> GetLeaveApprovalQueueAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct)
    {
        Require(caller, "LEAVE.APPROVE");

        var scope = await BuildScopeAsync(caller, query, ct);
        var staff = await LoadStaffAsync(scope, ct);

        var pending = await leave.QueryRequestsAsync(
            scope.TenantKey,
            new LeaveRequestFilter(null, scope.EmployeeIds, "Pending", null, null),
            ct);

        // Nobody decides their own leave. Marking the reader's own rows here means the queue
        // says so before the click rather than after the refusal.
        var own = await employees.GetByAccountAsync(scope.TenantKey, caller.UserId, ct);

        // Names and colours for the approval card: the type as HR named and coloured it, the
        // department as the master calls it. Inactive entries still name what they named.
        var policies = (await leave.GetPoliciesAsync(scope.TenantKey, false, ct)).ToDictionary(item => item.Id);
        var departments = (await masters.GetAllAsync(scope.TenantKey, ct))
            .Where(item => item.Category == "Department")
            .GroupBy(item => item.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

        var now = DateTimeOffset.UtcNow;
        var soonest = scope.AsOf.AddDays(StartingSoonDays);
        var rows = new List<LeaveApprovalRowDto>();

        foreach (var request in pending)
        {
            if (!staff.TryGetValue(request.EmployeeId, out var person))
            {
                // Outside the chosen company or unit. Not this scope's work.
                continue;
            }

            rows.Add(new LeaveApprovalRowDto(
                request.Id.ToString(CultureInfo.InvariantCulture),
                request.EmployeeId.ToString(CultureInfo.InvariantCulture),
                person.EmployeeNumber,
                DisplayName(person),
                person.DepartmentCode,
                person.JobTitle,
                request.LeaveTypeCode,
                request.FromDate,
                request.ToDate,
                request.Days,
                request.Reason,
                request.SubmittedUtc,
                Math.Max(0, (int)(now - request.SubmittedUtc).TotalDays),
                request.FromDate <= soonest,
                own is not null && own.Id == request.EmployeeId,
                policies.TryGetValue(request.PolicyId, out var policy) ? policy.Name : request.LeaveTypeCode,
                person.DepartmentCode is { Length: > 0 } department && departments.TryGetValue(department, out var departmentName)
                    ? departmentName
                    : person.DepartmentCode,
                Contracts.Mobile.Workforce.LeaveTones.Normalize(policy?.ToneKey),
                policy?.Emoji ?? Contracts.Mobile.Workforce.LeaveTones.DefaultEmoji));
        }

        // The urgent ones first, then the longest wait. Both are reasons to act now, and a
        // queue ordered by anything else is a queue nobody works from.
        var ordered = rows
            .OrderByDescending(row => row.StartsWithinWindow)
            .ThenByDescending(row => row.WaitingDays)
            .ThenBy(row => row.DisplayName)
            .ToList();

        return new LeaveApprovalQueueDto(
            scope.Dto,
            ordered,
            ordered.Count,
            ordered.Count(row => row.StartsWithinWindow),
            ordered.Count == 0 ? 0 : ordered.Max(row => row.WaitingDays));
    }

    // ---- Shared -----------------------------------------------------------

    private sealed record ResolvedScope(
        string TenantKey,
        DateOnly AsOf,
        EmployeeQueryFilter Filter,
        IReadOnlyList<long> EmployeeIds,
        DashboardScopeDto Dto);

    private async Task<ResolvedScope> ResolveScopeAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct)
    {
        Require(caller, ViewPermission);
        return await BuildScopeAsync(caller, query, ct);
    }

    /// <summary>
    /// The scope without the dashboard permission check, for the reads that carry a permission
    /// of their own. An approver who may clear the leave queue should not also have to hold
    /// DASHBOARD.VIEW to see the work assigned to them.
    /// </summary>
    private async Task<ResolvedScope> BuildScopeAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct)
    {
        // The client's today, not the server's — the same rule the dashboard follows, so the
        // drill-downs cannot land on a different date than the tile they were opened from.
        var today = await clock.TodayAsync(caller.TenantKey, ct);
        var asOf = query.AsOf ?? today;

        var filter = new EmployeeQueryFilter(
            null, null, null, null, null,
            CompanyCode: query.CompanyCode,
            UnitCode: query.UnitCode,
            ActiveOnly: true,

            // Same population rule as the dashboard: nobody is reported on before they start.
            JoinedOnOrBefore: asOf);

        var ids = await employees.GetIdsAsync(caller.TenantKey, filter, ct);

        return new ResolvedScope(
            caller.TenantKey, asOf, filter, ids,
            new DashboardScopeDto(
                query.CompanyCode, query.UnitCode, asOf, asOf != today, ids.Count,
                DateTimeOffset.UtcNow));
    }

    private async Task<Dictionary<long, EmployeeRecord>> LoadStaffAsync(
        ResolvedScope scope, CancellationToken ct)
    {
        var staff = await employees.QueryAsync(scope.TenantKey, scope.Filter, ct);
        return staff.ToDictionary(person => person.Id);
    }

    private static string DisplayName(EmployeeRecord person) =>
        string.IsNullOrWhiteSpace(person.PreferredName)
            ? $"{person.LegalFirstName} {person.LegalLastName}".Trim()
            : person.PreferredName;

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }

    private static (int Year, int Month) ResolveMonth(string? month, DateOnly asOf)
    {
        if (string.IsNullOrWhiteSpace(month) ||
            !DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            return (asOf.Year, asOf.Month);
        }

        return (parsed.Year, parsed.Month);
    }

    private static CursorPage<T> Page<T>(IReadOnlyList<T> items, string? cursor, int pageSize)
    {
        var size = pageSize <= 0 ? 25 : Math.Min(pageSize, 200);
        var offset = 0;

        if (!string.IsNullOrWhiteSpace(cursor) && int.TryParse(cursor, out var parsed) && parsed > 0)
        {
            offset = parsed;
        }

        var page = items.Skip(offset).Take(size).ToList();
        var next = offset + size;
        var hasMore = next < items.Count;

        return new CursorPage<T>(
            page,
            hasMore ? next.ToString(CultureInfo.InvariantCulture) : null,
            hasMore,
            size,
            items.Count);
    }
}
