using System.Globalization;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Dashboard;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Employee;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// The HR dashboard.
///
/// Two rules hold this together. Every widget is computed over the <em>same</em> population and
/// the <em>same</em> as-of date, resolved once per request — the prototype showed a header date
/// and a filter date four months apart, and that is exactly the failure this prevents. And every
/// number is derived from the tables at read time, so no tile can drift from the records it
/// summarises.
/// </summary>
public sealed class HrDashboardService(
    IEmployeeRepository employees,
    IAttendanceRepository attendance,
    ILeaveRepository leave,
    IShiftScheduleService schedule,
    Vispl.HRManagement.BM.ITF.Admin.IPolicyService policies,
    Vispl.HRManagement.BM.ITF.Shared.ITenantClock clock) : IHrDashboardService
{
    private const string ViewPermission = "DASHBOARD.VIEW";
    private const string MomentsPermission = "PEOPLE.MOMENTS.VIEW";
    private const string LeaveReportPermission = "LEAVE.REPORT";
    private const string AttendanceReportPermission = "ATTENDANCE.REPORT";
    private const string PolicyPermission = "POLICY.VIEW";


    /// <summary>
    /// Attendance bands. Held here as one list rather than as scattered comparisons, and
    /// returned with the rows so the on-screen legend cannot disagree with the calculation.
    /// </summary>
    private static readonly AttendanceBandDto[] Bands =
    [
        new("Excellent", 95m, "success"),
        new("Good", 85m, "success"),
        new("Fair", 75m, "warning"),
        new("Poor", 0m, "danger")
    ];

    /// <summary>
    /// Used when nothing was expected of somebody in the period, so there is no percentage to
    /// band. Deliberately not in <see cref="Bands"/>: it is not a threshold, and putting it in
    /// the legend would suggest attendance can be graded when it cannot.
    /// </summary>
    private static readonly AttendanceBandDto NotMeasured = new("Not measured", 0m, "muted");

    // ---- Composite --------------------------------------------------------

    public async Task<HrDashboardDto> GetDashboardAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(caller, query, ct);

        var headline = await BuildHeadlineAsync(caller, scope, ct);

        // A section the caller cannot see is absent, not empty. The client renders what it was
        // given instead of inferring permissions from an empty list.
        var moments = caller.Has(MomentsPermission)
            ? await BuildMomentsAsync(scope, 7, 7, ct)
            : null;

        var analytics = caller.Has(LeaveReportPermission)
            ? await BuildLeaveAnalyticsAsync(scope, ct)
            : null;

        var events = await BuildEventsAsync(scope, 30, ct);

        // Acknowledgement is per person, so the caller's own employee record is what decides
        // whether a card reads Read or Unread. A caller without one still sees the policies.
        var policySummaries = caller.Has(PolicyPermission)
            ? await policies.GetPublishedAsync(scope.TenantKey, caller.EmployeeId, ct)
            : null;

        return new HrDashboardDto(scope.Dto, headline, moments, analytics, events, policySummaries);
    }

    // ---- Headline ---------------------------------------------------------

    public async Task<DashboardHeadlineDto> GetHeadlineAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(caller, query, ct);
        return await BuildHeadlineAsync(caller, scope, ct);
    }

    private async Task<DashboardHeadlineDto> BuildHeadlineAsync(
        MobileCallerContext caller, ResolvedScope scope, CancellationToken ct)
    {
        var total = scope.EmployeeIds.Count;

        var statuses = await attendance.CountByStatusForAsync(
            scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct);

        var present = statuses.GetValueOrDefault("Present") + statuses.GetValueOrDefault("Partial");
        var onLeave = await leave.CountOnLeaveAsync(scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct);

        // Expected-to-work is the honest denominator: on a public holiday, presence as a
        // percentage of headcount reads like a catastrophe.
        //
        // Who is expected comes from each person's own shift, not from a company-wide Monday-to-
        // Friday assumption — a store shift runs on Sunday and an office one does not.
        var holidays = await leave.GetHolidaysAsync(scope.TenantKey, scope.AsOf, scope.AsOf, null, ct);
        var isHoliday = holidays.Count > 0;

        var shifts = await schedule.ResolveAsync(
            scope.TenantKey, scope.EmployeeIds, scope.AsOf, scope.AsOf, ct);

        var rostered = scope.EmployeeIds.Count(
            employeeId => shifts.For(employeeId, scope.AsOf)?.IsWorkingDay == true);

        var expected = isHoliday ? 0 : Math.Max(rostered - onLeave, 0);
        var isNonWorkingDay = expected == 0;

        var (absent, absentCaption) = await CountAbsentAsync(scope, shifts, isHoliday, ct);

        var (late, lateCaption) = await CountLateAsync(scope, ct);
        var pending = await leave.CountRequestsByStateAsync(
            scope.TenantKey, "Pending", scope.EmployeeIds, ct);

        var headcountLabel = $"of {total} employees";
        var expectedLabel = isNonWorkingDay
            ? (isHoliday ? "a public holiday — nobody was expected" : "nobody was rostered today")
            : $"of {expected} rostered today";

        var kpis = new List<HeadcountKpiDto>
        {
            new(DashboardKpiKeys.TotalEmployees, "Total Employees", total, null, null,
                headcountLabel, "info", "/employees", scope.Caption),

            new(DashboardKpiKeys.PresentToday, "Present Today", present,
                Percent(present, expected), expected, expectedLabel, "success", "/attendance", null),

            new(DashboardKpiKeys.OnLeave, "On Leave", onLeave,
                Percent(onLeave, total), total, headcountLabel, "info", "/leave", null),

            new(DashboardKpiKeys.Absent, absentCaption is null ? "Absent" : "Not In Yet", absent,
                Percent(absent, expected), expected, expectedLabel, "danger", "/attendance",
                absentCaption),

            // Lateness is measured against a fixed 09:00 until the shift module lands. The
            // caption says so rather than presenting an assumption as a fact.
            new(DashboardKpiKeys.LateComing, "Late Coming", late,
                Percent(late, expected), expected, expectedLabel, "warning",
                "/attendance/late", lateCaption),

            new(DashboardKpiKeys.PendingApproval, "Pending Approval", pending,
                null, null, "awaiting a decision", "accent", "/leave/team/requests", null)
        };

        return new DashboardHeadlineDto(scope.Dto, kpis);
    }

    /// <summary>
    /// Who was expected and did not appear — counted directly rather than inferred by
    /// subtracting the present from the rostered.
    ///
    /// The subtraction was wrong in a way nobody could see until the morning: before anybody
    /// has clocked in, "rostered minus present" is the whole company, so the dashboard opened
    /// every day announcing that everyone was absent. It also ignored each person's own shift,
    /// so a night-shift worker due at 22:00 was absent from 00:01.
    ///
    /// On a day still running, nobody is absent until their own shift has started and the
    /// grace on it has passed; before that they are simply not due yet. The tile says so, and
    /// the returned caption is what marks the figure as still moving.
    /// </summary>
    private async Task<(int Count, string? Caption)> CountAbsentAsync(
        ResolvedScope scope, ShiftSchedule shifts, bool isHoliday, CancellationToken ct)
    {
        if (isHoliday || scope.EmployeeIds.Count == 0)
        {
            return (0, null);
        }

        var arrived = await attendance.GetFirstInPunchAsync(
            scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct);

        var onLeave = (await leave.GetOnLeaveAsync(
            scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct)).ToHashSet();

        // A finished day is settled: every shift on it has started, so everyone rostered was
        // due. Only the day in progress needs the clock.
        var today = await clock.TodayAsync(scope.TenantKey, ct);
        var inProgress = scope.AsOf >= today;

        // Null means no shift has started yet, which is the whole truth about a date that has
        // not arrived: on a future day nobody is absent regardless of the time now. Comparing
        // only the time-of-day made everyone absent from tomorrow, but only after 09:15.
        TimeOnly? now = null;

        if (scope.AsOf == today)
        {
            var zone = await clock.ResolveZoneAsync(scope.TenantKey, ct);
            now = TimeOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        }
        else if (scope.AsOf > today)
        {
            now = TimeOnly.MinValue;
        }

        var absent = 0;
        var notDueYet = 0;

        foreach (var employeeId in scope.EmployeeIds)
        {
            if (shifts.For(employeeId, scope.AsOf) is not { IsWorkingDay: true } shift)
            {
                continue;
            }

            if (onLeave.Contains(employeeId) || arrived.ContainsKey(employeeId))
            {
                continue;
            }

            if (now is { } clockTime && clockTime < shift.StartTime.AddMinutes(shift.GraceMinutes))
            {
                notDueYet++;
                continue;
            }

            absent++;
        }

        // Said only while the day can still change the answer. On a closed day the number is
        // final, and on a day nobody was rostered for there is nothing provisional about zero —
        // a hedge on either would be noise.
        var caption = !inProgress || (absent == 0 && notDueYet == 0) ? null
            : notDueYet > 0
                ? $"So far today — {notDueYet} not due in yet"
                : "So far today";

        return (absent, caption);
    }

    /// <summary>
    /// Late arrivals measured against each person's own shift, plus the caption the tile shows.
    /// The caption is only set when the figure rests on the default shift rather than a roster.
    /// </summary>
    private async Task<(int Count, string? Caption)> CountLateAsync(
        ResolvedScope scope, CancellationToken ct)
    {
        var firstPunches = await attendance.GetFirstInPunchAsync(
            scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct);

        var shifts = await schedule.ResolveAsync(
            scope.TenantKey, scope.EmployeeIds, scope.AsOf, scope.AsOf, ct);

        var late = firstPunches.Count(
            punch => shifts.DelayMinutes(punch.Key, scope.AsOf, punch.Value) > 0);

        var caption = shifts.UsesFallbackOnly && shifts.Fallback is { } fallback
            ? $"Against the default {fallback.StartTime:HH\\:mm} shift"
            : null;

        return (late, caption);
    }

    // ---- People moments ---------------------------------------------------

    public async Task<PeopleMomentsDto> GetPeopleMomentsAsync(
        MobileCallerContext caller, PeopleMomentsQuery query, CancellationToken ct)
    {
        Require(caller, MomentsPermission);
        var scope = await ResolveScopeAsync(caller, query, ct);
        return await BuildMomentsAsync(scope, query.BelatedDays, query.UpcomingDays, ct);
    }

    private async Task<PeopleMomentsDto> BuildMomentsAsync(
        ResolvedScope scope, int belated, int upcoming, CancellationToken ct)
    {
        var birthdays = await employees.GetMomentsAsync(
            scope.TenantKey, scope.Filter, scope.AsOf, belated, upcoming, anniversaries: false, ct);

        var anniversaries = await employees.GetMomentsAsync(
            scope.TenantKey, scope.Filter, scope.AsOf, belated, upcoming, anniversaries: true, ct);

        return new PeopleMomentsDto(
            scope.Dto,
            birthdays.Select(MapMoment).ToList(),
            anniversaries.Select(MapMoment).ToList(),
            belated,
            upcoming);
    }

    // ---- Leave analytics --------------------------------------------------

    public async Task<LeaveAnalyticsDto> GetLeaveAnalyticsAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct)
    {
        Require(caller, LeaveReportPermission);
        var scope = await ResolveScopeAsync(caller, query, ct);
        return await BuildLeaveAnalyticsAsync(scope, ct);
    }

    private async Task<LeaveAnalyticsDto> BuildLeaveAnalyticsAsync(
        ResolvedScope scope, CancellationToken ct)
    {
        // The whole leave year, not year-to-date. Leave is planned forward: a request approved
        // today for next month is real, and a status split that ended at today would omit it
        // while the Pending Approval tile counted it. The window travels on the DTO so the
        // screen can label what it is showing.
        var from = new DateOnly(scope.AsOf.Year, 1, 1);
        var to = new DateOnly(scope.AsOf.Year, 12, 31);

        // The trend still looks backwards from the as-of date, six months as the prototype does.
        var trendFrom = new DateOnly(scope.AsOf.Year, scope.AsOf.Month, 1).AddMonths(-5);

        var policies = await leave.GetPoliciesAsync(scope.TenantKey, true, ct);
        var byPolicy = await leave.SumDaysByPolicyAsync(scope.TenantKey, from, to, scope.EmployeeIds, ct);
        var byMonth = await leave.SumDaysByMonthAsync(scope.TenantKey, trendFrom, scope.AsOf, scope.EmployeeIds, ct);
        var byState = await leave.CountByStateAsync(scope.TenantKey, from, to, scope.EmployeeIds, ct);

        // Generated from the active policies, not from a hard-coded four: a tenant may run any
        // number of leave types, and a policy with no days taken is a real answer worth showing.
        var distribution = policies
            .Select(policy => new LeaveCategorySliceDto(
                policy.Code, policy.Name, byPolicy.GetValueOrDefault(policy.Id)))
            .ToList();

        var trend = new List<LeaveTrendPointDto>();
        for (var month = trendFrom; month <= scope.AsOf; month = month.AddMonths(1))
        {
            var match = byMonth.FirstOrDefault(item => item.Year == month.Year && item.Month == month.Month);
            trend.Add(new LeaveTrendPointDto(
                month.Year,
                month.Month,
                CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(month.Month),
                match.Days));
        }

        var split = new LeaveStatusSplitDto(
            byState.GetValueOrDefault("Approved"),
            byState.GetValueOrDefault("Pending"),
            byState.GetValueOrDefault("Rejected"),
            byState.GetValueOrDefault("Withdrawn"));

        return new LeaveAnalyticsDto(scope.Dto, from, to, distribution, trend, split);
    }

    // ---- Attendance overview ----------------------------------------------

    public async Task<AttendanceOverviewDto> GetAttendanceOverviewAsync(
        MobileCallerContext caller, AttendanceOverviewQuery query, CancellationToken ct)
    {
        Require(caller, AttendanceReportPermission);

        var scope = await ResolveScopeAsync(caller, query, ct);
        var (year, month) = ResolveMonth(query.Month, scope.AsOf);

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var staff = await employees.QueryAsync(scope.TenantKey, scope.Filter, ct);

        var days = await attendance.QueryDaysAsync(
            scope.TenantKey,
            new AttendanceDayFilter(from, to, null, scope.EmployeeIds, null, false),
            ct);

        var leaveDays = await leave.SumDaysByEmployeeAsync(
            scope.TenantKey, from, to, scope.EmployeeIds, ct);

        var holidays = await leave.GetHolidaysAsync(scope.TenantKey, from, to, null, ct);
        var holidayDates = holidays.Select(item => item.Date).ToHashSet();

        // Expected days are counted per person from their own shift, so someone on a seven-day
        // store rota is not measured against a five-day office month.
        var monthShifts = await schedule.ResolveAsync(
            scope.TenantKey, scope.EmployeeIds, from, to, ct);

        var byEmployee = days.GroupBy(day => day.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var rows = new List<EmployeeAttendanceRowDto>();

        foreach (var person in staff)
        {
            // Somebody who had not joined by the end of this month was not employed for any of
            // it. A row of zeros for a month they were not here reads as a full month missed.
            if (person.JoinedOn > to)
            {
                continue;
            }

            var personDays = byEmployee.GetValueOrDefault(person.Id) ?? [];
            var present = personDays.Count(day => day.Status == "Present");
            var partial = personDays.Count(day => day.Status == "Partial");
            var absent = personDays.Count(day => day.Status == "Absent");
            var onLeave = leaveDays.GetValueOrDefault(person.Id);

            // A half day counts as half a day present, so the percentage matches what the
            // employee's own screen shows them.
            //
            // Counting starts on the joining date, not the first of the month: a person who
            // started on the 25th owes four days, not twenty-two, and measuring them against
            // the whole month makes every new joiner look like the worst attender in the
            // company for their first month.
            var countFrom = person.JoinedOn > from ? person.JoinedOn : from;

            var expected = 0;
            for (var date = countFrom; date <= to; date = date.AddDays(1))
            {
                if (holidayDates.Contains(date))
                {
                    continue;
                }

                if (monthShifts.For(person.Id, date)?.IsWorkingDay == true)
                {
                    expected++;
                }
            }

            var attended = present + (partial * 0.5m);
            var percent = expected == 0 ? 0m : Math.Round(attended * 100m / expected, 1);

            // Nothing was expected — they joined after the last working day of the month, or
            // every day of it was a holiday. There is no attendance to grade, and calling that
            // "Poor" invents a judgement the data does not support.
            var band = expected == 0 ? NotMeasured : Bands.First(item => percent >= item.MinPercent);

            rows.Add(new EmployeeAttendanceRowDto(
                person.Id.ToString(CultureInfo.InvariantCulture),
                $"{person.LegalFirstName} {person.LegalLastName}".Trim(),
                person.DepartmentCode,
                person.JobTitle,
                present,
                absent,
                onLeave,
                expected,
                percent,
                band.Band,
                band.Tone));
        }

        var ordered = rows.OrderByDescending(row => row.AttendancePercent).ToList();

        return new AttendanceOverviewDto(
            scope.Dto, year, month, Page(ordered, query.Cursor, query.PageSize), Bands);
    }

    // ---- KPI drill-down -----------------------------------------------------

    public async Task<DashboardKpiMembersDto> GetKpiMembersAsync(
        MobileCallerContext caller, string key, DashboardScopeQuery query, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(caller, query, ct);
        var staff = await employees.QueryAsync(scope.TenantKey, scope.Filter, ct);
        var byId = staff.ToDictionary(person => person.Id);

        IReadOnlyList<DashboardKpiMemberDto> rows = key switch
        {
            DashboardKpiKeys.TotalEmployees => TotalEmployeesRows(staff),
            DashboardKpiKeys.PresentToday => await PresentTodayRowsAsync(scope, byId, ct),
            DashboardKpiKeys.OnLeave => await OnLeaveRowsAsync(scope, byId, ct),
            DashboardKpiKeys.Absent => await AbsentRowsAsync(scope, byId, ct),
            DashboardKpiKeys.LateComing => await LateComingRowsAsync(scope, byId, ct),
            DashboardKpiKeys.PendingApproval => await PendingApprovalRowsAsync(scope, byId, ct),
            _ => throw new MobileValidationException("KPI_UNKNOWN", "Unknown dashboard KPI.")
        };

        var title = key switch
        {
            DashboardKpiKeys.TotalEmployees => "Total Employees",
            DashboardKpiKeys.PresentToday => "Present Today",
            DashboardKpiKeys.OnLeave => "On Leave",
            DashboardKpiKeys.Absent => "Not In Yet",
            DashboardKpiKeys.LateComing => "Late Coming",
            DashboardKpiKeys.PendingApproval => "Pending Approval",
            _ => key
        };

        return new DashboardKpiMembersDto(scope.Dto, key, title, rows);
    }

    private static DashboardKpiMemberDto Row(
        long employeeId, IReadOnlyDictionary<long, EmployeeRecord> byId, string detail, string tone)
    {
        byId.TryGetValue(employeeId, out var person);
        var name = person is null
            ? employeeId.ToString(CultureInfo.InvariantCulture)
            : $"{person.LegalFirstName} {person.LegalLastName}".Trim();

        return new DashboardKpiMemberDto(
            employeeId.ToString(CultureInfo.InvariantCulture),
            name,
            person?.DepartmentCode,
            person?.JobTitle,
            detail,
            tone);
    }

    private static IReadOnlyList<DashboardKpiMemberDto> TotalEmployeesRows(IReadOnlyList<EmployeeRecord> staff) =>
        staff
            .OrderBy(person => person.LegalFirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(person => person.LegalLastName, StringComparer.OrdinalIgnoreCase)
            .Select(person => new DashboardKpiMemberDto(
                person.Id.ToString(CultureInfo.InvariantCulture),
                $"{person.LegalFirstName} {person.LegalLastName}".Trim(),
                person.DepartmentCode,
                person.JobTitle,
                $"Joined {person.JoinedOn:dd MMM yyyy}",
                "info"))
            .ToList();

    private async Task<IReadOnlyList<DashboardKpiMemberDto>> PresentTodayRowsAsync(
        ResolvedScope scope, IReadOnlyDictionary<long, EmployeeRecord> byId, CancellationToken ct)
    {
        var days = await attendance.QueryDaysAsync(
            scope.TenantKey,
            new AttendanceDayFilter(scope.AsOf, scope.AsOf, null, scope.EmployeeIds, null, false),
            ct);

        var firstPunches = await attendance.GetFirstInPunchAsync(scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct);
        var zone = await clock.ResolveZoneAsync(scope.TenantKey, ct);

        return days
            .Where(day => day.Status is "Present" or "Partial")
            .Select(day =>
            {
                var detail = day.Status == "Partial"
                    ? "Partial day"
                    : firstPunches.TryGetValue(day.EmployeeId, out var arrived)
                        ? $"In at {TimeZoneInfo.ConvertTime(arrived, zone):hh\\:mm tt}"
                        : "Present";

                return Row(day.EmployeeId, byId, detail, day.Status == "Partial" ? "warning" : "success");
            })
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<DashboardKpiMemberDto>> OnLeaveRowsAsync(
        ResolvedScope scope, IReadOnlyDictionary<long, EmployeeRecord> byId, CancellationToken ct)
    {
        var onLeaveIds = await leave.GetOnLeaveAsync(scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct);

        return onLeaveIds
            .Select(employeeId => Row(employeeId, byId, "On leave today", "info"))
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Mirrors <see cref="CountAbsentAsync"/> row for row, so this list and the tile it drills
    /// into can never disagree on who counts as absent.
    /// </summary>
    private async Task<IReadOnlyList<DashboardKpiMemberDto>> AbsentRowsAsync(
        ResolvedScope scope, IReadOnlyDictionary<long, EmployeeRecord> byId, CancellationToken ct)
    {
        if (scope.EmployeeIds.Count == 0)
        {
            return [];
        }

        var holidays = await leave.GetHolidaysAsync(scope.TenantKey, scope.AsOf, scope.AsOf, null, ct);
        if (holidays.Count > 0)
        {
            return [];
        }

        var shifts = await schedule.ResolveAsync(scope.TenantKey, scope.EmployeeIds, scope.AsOf, scope.AsOf, ct);
        var arrived = await attendance.GetFirstInPunchAsync(scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct);
        var onLeave = (await leave.GetOnLeaveAsync(scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct)).ToHashSet();

        var today = await clock.TodayAsync(scope.TenantKey, ct);
        var inProgress = scope.AsOf >= today;

        TimeOnly? now = null;

        if (scope.AsOf == today)
        {
            var zone = await clock.ResolveZoneAsync(scope.TenantKey, ct);
            now = TimeOnly.FromDateTime(
                TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        }
        else if (scope.AsOf > today)
        {
            now = TimeOnly.MinValue;
        }

        var rows = new List<DashboardKpiMemberDto>();

        foreach (var employeeId in scope.EmployeeIds)
        {
            if (shifts.For(employeeId, scope.AsOf) is not { IsWorkingDay: true } shift)
            {
                continue;
            }

            if (onLeave.Contains(employeeId) || arrived.ContainsKey(employeeId))
            {
                continue;
            }

            if (now is { } clockTime && clockTime < shift.StartTime.AddMinutes(shift.GraceMinutes))
            {
                // Not due yet — the tile does not count them as absent, so neither does this list.
                continue;
            }

            rows.Add(Row(employeeId, byId, inProgress ? "Not in yet" : "Absent", "danger"));
        }

        return rows.OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<IReadOnlyList<DashboardKpiMemberDto>> LateComingRowsAsync(
        ResolvedScope scope, IReadOnlyDictionary<long, EmployeeRecord> byId, CancellationToken ct)
    {
        var firstPunches = await attendance.GetFirstInPunchAsync(scope.TenantKey, scope.AsOf, scope.EmployeeIds, ct);
        var shifts = await schedule.ResolveAsync(scope.TenantKey, scope.EmployeeIds, scope.AsOf, scope.AsOf, ct);
        var zone = await clock.ResolveZoneAsync(scope.TenantKey, ct);

        var rows = new List<DashboardKpiMemberDto>();

        foreach (var (employeeId, firstIn) in firstPunches)
        {
            var delay = shifts.DelayMinutes(employeeId, scope.AsOf, firstIn);
            if (delay <= 0)
            {
                continue;
            }

            var local = TimeZoneInfo.ConvertTime(firstIn, zone);
            rows.Add(Row(employeeId, byId, $"In at {local:hh\\:mm tt} · {delay} min late", "warning"));
        }

        return rows.OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private async Task<IReadOnlyList<DashboardKpiMemberDto>> PendingApprovalRowsAsync(
        ResolvedScope scope, IReadOnlyDictionary<long, EmployeeRecord> byId, CancellationToken ct)
    {
        var requests = await leave.QueryRequestsAsync(
            scope.TenantKey,
            new LeaveRequestFilter(null, scope.EmployeeIds, "Pending", null, null),
            ct);

        return requests
            .Select(request => Row(
                request.EmployeeId, byId,
                $"{request.LeaveTypeCode} · {request.FromDate:dd MMM} - {request.ToDate:dd MMM} · {request.Days:0.#} day(s)",
                "warning"))
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // ---- Events -----------------------------------------------------------

    public async Task<IReadOnlyList<CompanyEventDto>> GetUpcomingEventsAsync(
        MobileCallerContext caller, DashboardScopeQuery query, int windowDays, CancellationToken ct)
    {
        var scope = await ResolveScopeAsync(caller, query, ct);
        return await BuildEventsAsync(scope, windowDays, ct);
    }

    private async Task<IReadOnlyList<CompanyEventDto>> BuildEventsAsync(
        ResolvedScope scope, int windowDays, CancellationToken ct)
    {
        var holidays = await leave.GetHolidaysAsync(
            scope.TenantKey, scope.AsOf, scope.AsOf.AddDays(windowDays), null, ct);

        // The holiday record carries no type or description yet, so every event reports as a
        // holiday rather than inventing a category it cannot know.
        return holidays
            .Select(holiday => new CompanyEventDto(
                holiday.Id.ToString(CultureInfo.InvariantCulture),
                holiday.Name,
                holiday.Date,
                "Holiday",
                null,
                holiday.LocationCode))
            .ToList();
    }

    // ---- Scope ------------------------------------------------------------

    private sealed record ResolvedScope(
        string TenantKey,
        DateOnly AsOf,
        EmployeeQueryFilter Filter,
        IReadOnlyList<long> EmployeeIds,
        string? Caption,
        DashboardScopeDto Dto);

    /// <summary>
    /// Resolves the population once per request. Every widget is then computed over the same
    /// list, which is what stops two cards on one page describing two different companies.
    /// </summary>
    private async Task<ResolvedScope> ResolveScopeAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct)
    {
        Require(caller, ViewPermission);

        // The client's today, not the server's. At half past midnight in Chandigarh a UTC clock
        // still says yesterday, so the dashboard would open on the wrong date and then judge a
        // finished day as if it were still running.
        var today = await clock.TodayAsync(caller.TenantKey, ct);
        var asOf = query.AsOf ?? today;

        var filter = new EmployeeQueryFilter(
            null, null, null, null, null,
            CompanyCode: query.CompanyCode,
            UnitCode: query.UnitCode,
            ActiveOnly: true,

            // The population is who was employed on the date being shown. Without this a
            // future joiner is counted in headcount and, because absence is rostered minus
            // present, marked absent from a job they have not started.
            JoinedOnOrBefore: asOf);

        var ids = await employees.GetIdsAsync(caller.TenantKey, filter, ct);

        var caption = query.CompanyCode is null
            ? "Across all companies"
            : $"Company {query.CompanyCode}";

        var dto = new DashboardScopeDto(
            query.CompanyCode,
            query.UnitCode,
            asOf,
            asOf != today,
            ids.Count,
            DateTimeOffset.UtcNow);

        return new ResolvedScope(caller.TenantKey, asOf, filter, ids, caption, dto);
    }

    // ---- Helpers ----------------------------------------------------------

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }

    private static decimal? Percent(int part, int whole) =>
        whole <= 0 ? null : Math.Round(part * 100m / whole, 2);

    private static PersonMomentDto MapMoment(PersonMomentRecord moment) => new(
        moment.EmployeeId.ToString(CultureInfo.InvariantCulture),
        moment.DisplayName,
        moment.AvatarUrl,
        moment.DepartmentCode,
        moment.JobTitle,
        moment.Date,
        moment.Years,
        moment.Window);

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
