using System.Globalization;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Admin;
using Vispl.HRManagement.VO.Employee;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Admin;

public sealed class EmployeeReportService(
    IEmployeeRepository employees,
    IMasterOptionRepository masters,
    IAttendanceRepository attendance,
    IPayrollRepository payroll,
    IShiftScheduleService schedule,
    ILeaveRepository leave,
    Vispl.HRManagement.BM.ITF.Shared.ITenantClock clock) : IEmployeeReportService
{
    private const string EmployeesView = "EMPLOYEES.VIEW";
    private const string ReportsView = "REPORTS.VIEW";
    private const string AttendanceView = "ATTENDANCE.VIEW";
    private const string AttendanceManage = "ATTENDANCE.MANAGE";
    private const string PayrollView = "PAYROLL.VIEW";

    // ---- Directory (Employee Detail Report) ---------------------------------

    public async Task<IReadOnlyList<EmployeeDirectoryRowDto>> GetDirectoryAsync(
        MobileCallerContext caller, EmployeeReportQuery query, CancellationToken cancellationToken = default)
    {
        Require(caller, EmployeesView);

        var (_, rows) = await LoadDirectoryAsync(caller.TenantKey, cancellationToken);
        return rows.Where(row => Matches(query, row)).ToList();
    }

    // ---- Organisation Hierarchy ---------------------------------------------

    public async Task<OrgHierarchyDto> GetHierarchyAsync(
        MobileCallerContext caller, EmployeeReportQuery query, CancellationToken cancellationToken = default)
    {
        Require(caller, EmployeesView);

        var (_, all) = await LoadDirectoryAsync(caller.TenantKey, cancellationToken);
        var rows = all.Where(row => Matches(query, row)).ToList();
        var inScope = rows.Select(row => row.EmployeeId).ToHashSet();

        // Someone whose manager is outside the filtered set still belongs on the chart — as a root.
        var childrenOf = rows
            .Where(row => row.ManagerId is not null && inScope.Contains(row.ManagerId))
            .GroupBy(row => row.ManagerId!)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => row.DisplayName).ToList());

        var visited = new HashSet<string>();
        var maxDepth = 0;

        OrgHierarchyNodeDto Build(EmployeeDirectoryRowDto row, int depth)
        {
            visited.Add(row.EmployeeId);
            maxDepth = Math.Max(maxDepth, depth);

            // A reporting cycle is refused on write, but old data is not trusted: never loop.
            var reports = childrenOf.GetValueOrDefault(row.EmployeeId, [])
                .Where(child => !visited.Contains(child.EmployeeId))
                .Select(child => Build(child, depth + 1))
                .ToList();

            return new OrgHierarchyNodeDto(row, reports, reports.Sum(node => node.TeamSize + 1));
        }

        var roots = rows
            .Where(row => row.ManagerId is null || !inScope.Contains(row.ManagerId))
            .OrderByDescending(row => childrenOf.ContainsKey(row.EmployeeId))
            .ThenBy(row => row.DisplayName)
            .Select(row => Build(row, 1))
            .ToList();

        // Anything left unvisited sits inside a reporting cycle; surface it rather than hide it.
        roots.AddRange(rows.Where(row => !visited.Contains(row.EmployeeId)).Select(row => Build(row, 1)));

        var divisions = rows
            .GroupBy(row => row.DivisionCode, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.First().DivisionName)
            .Select(division => new OrgDivisionGroupDto(
                division.Key,
                division.First().DivisionName,
                division.Count(),
                division
                    .GroupBy(row => row.DepartmentCode, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(group => group.First().DepartmentName)
                    .Select(department => new OrgDepartmentGroupDto(
                        department.Key,
                        department.First().DepartmentName,
                        department.OrderBy(row => row.DisplayName).ToList()))
                    .ToList()))
            .ToList();

        return new OrgHierarchyDto(
            rows.Count,
            rows.Count(row => childrenOf.ContainsKey(row.EmployeeId)),
            rows.Count(row => row.ManagerId is null),
            rows.Count == 0 ? 0 : maxDepth,
            roots,
            divisions);
    }

    // ---- Employee Monthly Attendance Report ----------------------------------

    public async Task<MonthlyAttendanceReportDto> GetMonthlyAttendanceAsync(
        MobileCallerContext caller, EmployeeReportQuery query, CancellationToken cancellationToken = default)
    {
        Require(caller, ReportsView);
        Require(caller, AttendanceView);
        var (first, last) = MonthOf(query);

        var (records, rows) = await LoadDirectoryAsync(caller.TenantKey, cancellationToken);
        var recordsById = records.ToDictionary(record => record.Id.ToString(CultureInfo.InvariantCulture));
        var people = rows
            .Where(row => row.JoinedOn <= last && Matches(query, row))
            .Where(row => Blank(query.CompanyCode) ||
                          Same(recordsById.GetValueOrDefault(row.EmployeeId)?.CompanyCode ?? string.Empty, query.CompanyCode))
            .ToList();

        var ids = people.Select(row => long.Parse(row.EmployeeId, CultureInfo.InvariantCulture)).ToList();
        var days = ids.Count == 0
            ? []
            : await attendance.QueryDaysAsync(
                caller.TenantKey, new AttendanceDayFilter(first, last, null, ids, null, false), cancellationToken);
        var byEmployee = days
            .GroupBy(day => day.EmployeeId)
            .ToDictionary(group => group.Key, group => group.GroupBy(day => day.WorkDate).ToDictionary(item => item.Key, item => item.First()));

        // In / out times, shifts, leave codes and companies for the Detailed Attendance Records grid.
        var punches = (await attendance.GetPunchesForAsync(caller.TenantKey, ids, first, last, cancellationToken))
            .GroupBy(punch => (punch.EmployeeId, punch.WorkDate))
            .ToDictionary(group => group.Key, group => group.OrderBy(punch => punch.OccurredUtc).ToList());
        var shifts = ids.Count == 0 ? null : await schedule.ResolveAsync(caller.TenantKey, ids, first, last, cancellationToken);
        var zone = await clock.ResolveZoneAsync(caller.TenantKey, cancellationToken);
        var today = await clock.TodayAsync(caller.TenantKey, cancellationToken);
        var leaveDays = await LoadLeaveDaysAsync(caller.TenantKey, ids, first, last, cancellationToken);
        var companies = (await employees.GetScopeOptionsAsync(caller.TenantKey, null, cancellationToken)).Companies
            .GroupBy(company => company.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);
        var canEdit = caller.Has(AttendanceManage);

        // P, A, L, W per calendar day, across everyone in scope — the calendar's four dots.
        var totals = new Dictionary<DateOnly, int[]>();
        var reportRows = new List<AttendanceReportRowDto>();

        foreach (var person in people)
        {
            var id = long.Parse(person.EmployeeId, CultureInfo.InvariantCulture);
            var recorded = byEmployee.GetValueOrDefault(id) ?? [];
            var codes = new List<string>();
            var cells = new List<AttendanceReportCellDto>();
            int present = 0, partial = 0, absent = 0, leave = 0, holiday = 0, weeklyOff = 0, unrecorded = 0;

            for (var date = first; date <= last; date = date.AddDays(1))
            {
                string code;
                string cellCode;
                string tone;
                TimeOnly? checkIn = null;
                TimeOnly? checkOut = null;

                if (date < person.JoinedOn || date > today)
                {
                    code = AttendanceDayCodes.NotApplicable;
                    cellCode = string.Empty;
                    tone = "na";
                }
                else
                {
                    var dayPunches = punches.GetValueOrDefault((id, date)) ?? [];
                    checkIn = LocalTime(dayPunches.FirstOrDefault(punch => IsType(punch, "In")), zone);
                    checkOut = LocalTime(dayPunches.LastOrDefault(punch => IsType(punch, "Out")), zone);

                    recorded.TryGetValue(date, out var day);
                    var worked = day?.Status is "Present" or "Partial";

                    if (!worked && leaveDays.TryGetValue((id, date), out var leaveCode))
                    {
                        // Approved leave shows as its policy code (SL, CL…), not a bare L.
                        (code, cellCode, tone) = (AttendanceDayCodes.Leave, leaveCode, "leave");
                    }
                    else if (day is not null)
                    {
                        (code, cellCode, tone) = day.Status switch
                        {
                            "Present" => (AttendanceDayCodes.Present, "P", "present"),
                            "Partial" => (AttendanceDayCodes.Partial, HalfDayCode(shifts?.For(id, date), checkIn), "half-day"),
                            "Absent" => (AttendanceDayCodes.Absent, "A", "absent"),
                            "Leave" => (AttendanceDayCodes.Leave, "L", "leave"),
                            "Holiday" => (AttendanceDayCodes.Holiday, "H", "holiday"),
                            "WeeklyOff" => (AttendanceDayCodes.WeeklyOff, "W", "weekly-off"),
                            _ => (AttendanceDayCodes.Unrecorded, "-", "unrecorded")
                        };
                    }
                    else if (date.DayOfWeek == DayOfWeek.Sunday)
                    {
                        // Matches payroll: an unrecorded Sunday is a weekly off, not a missing day.
                        (code, cellCode, tone) = (AttendanceDayCodes.WeeklyOff, "W", "weekly-off");
                    }
                    else
                    {
                        (code, cellCode, tone) = (AttendanceDayCodes.Unrecorded, "-", "unrecorded");
                    }
                }

                switch (code)
                {
                    case AttendanceDayCodes.Present: present++; break;
                    case AttendanceDayCodes.Partial: partial++; break;
                    case AttendanceDayCodes.Absent: absent++; break;
                    case AttendanceDayCodes.Leave: leave++; break;
                    case AttendanceDayCodes.Holiday: holiday++; break;
                    case AttendanceDayCodes.WeeklyOff: weeklyOff++; break;
                    case AttendanceDayCodes.Unrecorded: unrecorded++; break;
                }

                if (code != AttendanceDayCodes.NotApplicable)
                {
                    if (!totals.TryGetValue(date, out var bucket))
                    {
                        bucket = new int[4];
                        totals[date] = bucket;
                    }

                    switch (code)
                    {
                        case AttendanceDayCodes.Present or AttendanceDayCodes.Partial: bucket[0]++; break;
                        case AttendanceDayCodes.Absent: bucket[1]++; break;
                        case AttendanceDayCodes.Leave: bucket[2]++; break;
                        case AttendanceDayCodes.WeeklyOff or AttendanceDayCodes.Holiday: bucket[3]++; break;
                    }
                }

                codes.Add(code);
                cells.Add(new AttendanceReportCellDto(date, cellCode, tone, checkIn, checkOut, canEdit && tone != "na"));
            }

            var record = recordsById.GetValueOrDefault(person.EmployeeId);

            reportRows.Add(new AttendanceReportRowDto(
                person.EmployeeId,
                person.EmployeeNumber,
                person.DisplayName,
                person.DivisionName,
                person.DepartmentName,
                present, partial, absent, leave, holiday, weeklyOff, unrecorded,
                Math.Round(recorded.Values.Sum(day => day.WorkedMinutes) / 60m, 1),
                codes,
                record?.CompanyCode is { Length: > 0 } company ? companies.GetValueOrDefault(company, company) : null,
                person.JobTitle,
                ShiftLabel(shifts, id, first, last),
                cells));
        }

        var initials = Enumerable.Range(0, last.Day)
            .Select(offset => first.AddDays(offset).DayOfWeek.ToString()[..1])
            .ToList();

        // Rate over the days someone could have worked: leave and weekly offs are not held against it.
        var presentDays = reportRows.Sum(row => row.Present + row.Partial);
        var absentDays = reportRows.Sum(row => row.Absent);
        var rate = presentDays + absentDays == 0
            ? 0
            : Math.Round((reportRows.Sum(row => row.Present) + reportRows.Sum(row => row.Partial) * 0.5m)
                         / (presentDays + absentDays) * 100m, 1);

        var dailyTotals = Enumerable.Range(0, last.Day)
            .Select(offset =>
            {
                var date = first.AddDays(offset);
                var bucket = totals.GetValueOrDefault(date) ?? new int[4];
                return new AttendanceDailyTotalDto(date, bucket[0], bucket[1], bucket[2], bucket[3]);
            })
            .ToList();

        return new MonthlyAttendanceReportDto(
            query.Month, query.Year, MonthLabel(first), last.Day, initials,
            reportRows.OrderBy(row => row.DivisionName).ThenBy(row => row.DepartmentName).ThenBy(row => row.DisplayName).ToList(),
            new AttendanceReportSummaryDto(presentDays, absentDays, reportRows.Sum(row => row.Leave), rate),
            dailyTotals,
            canEdit);
    }

    /// <summary>Approved leave per employee-day, as the policy code (SL, CL…).</summary>
    private async Task<Dictionary<(long EmployeeId, DateOnly Date), string>> LoadLeaveDaysAsync(
        string tenantKey, IReadOnlyList<long> ids, DateOnly first, DateOnly last, CancellationToken ct)
    {
        if (ids.Count == 0)
        {
            return new();
        }

        var requests = await leave.QueryRequestsAsync(tenantKey, new LeaveRequestFilter(null, ids, "Approved", first, last), ct);
        if (requests.Count == 0)
        {
            return new();
        }

        var codes = requests.ToDictionary(request => request.Id, request => request.LeaveTypeCode);
        var dayRows = await leave.GetDaysForRequestsAsync(tenantKey, requests.Select(request => request.Id).ToList(), ct);

        return dayRows
            .Where(day => day.Date >= first && day.Date <= last)
            .GroupBy(day => (day.EmployeeId, day.Date))
            .ToDictionary(group => group.Key, group => codes.GetValueOrDefault(group.First().RequestId, AttendanceDayCodes.Leave));
    }

    /// <summary>HDF when the half worked was the first half of the shift, HDS when it started past the midpoint.</summary>
    private static string HalfDayCode(ExpectedShiftRecord? shift, TimeOnly? checkIn)
    {
        if (shift is null || checkIn is null)
        {
            return "HDF";
        }

        var midpoint = shift.StartTime.Add((shift.EndTime - shift.StartTime) / 2);
        return checkIn.Value >= midpoint.AddMinutes(-30) ? "HDS" : "HDF";
    }

    private static string? ShiftLabel(ShiftSchedule? shifts, long employeeId, DateOnly first, DateOnly last)
    {
        if (shifts is null)
        {
            return null;
        }

        for (var date = first; date <= last; date = date.AddDays(1))
        {
            if (shifts.For(employeeId, date) is { IsWorkingDay: true } expected)
            {
                return $"{expected.StartTime:HH:mm}–{expected.EndTime:HH:mm} ({expected.ShiftName})";
            }
        }

        return shifts.Fallback is { } fallback
            ? $"{fallback.StartTime:HH:mm}–{fallback.EndTime:HH:mm} ({fallback.Name})"
            : null;
    }

    private static TimeOnly? LocalTime(AttendancePunchRecord? punch, TimeZoneInfo zone) =>
        punch is null ? null : TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(punch.OccurredUtc, zone).DateTime);

    private static bool IsType(AttendancePunchRecord punch, string type) =>
        string.Equals(punch.Type, type, StringComparison.OrdinalIgnoreCase);

    // ---- Employees Salary Register ------------------------------------------

    public async Task<SalaryRegisterDto> GetSalaryRegisterAsync(
        MobileCallerContext caller, EmployeeReportQuery query, CancellationToken cancellationToken = default)
    {
        Require(caller, ReportsView);
        Require(caller, PayrollView);
        var (first, _) = MonthOf(query);

        var run = await payroll.GetRunAsync(caller.TenantKey, query.Year, query.Month, PayrollSalaryTypes.Monthly, cancellationToken);
        if (run is null)
        {
            return new SalaryRegisterDto(query.Month, query.Year, MonthLabel(first), false, false, null, [], PayrollAmountsDto.Zero);
        }

        var options = await masters.GetAllAsync(caller.TenantKey, cancellationToken);
        var divisionNames = NamesFor(options, MasterCategories.Division);
        var departmentNames = NamesFor(options, MasterCategories.Department);

        var lines = await payroll.GetLinesAsync(caller.TenantKey, run.Id, cancellationToken);
        var search = query.Search?.Trim();

        var rows = lines
            .Where(line => Blank(query.DivisionCode) || Same(line.DivisionCode, query.DivisionCode))
            .Where(line => Blank(query.DepartmentCode) || Same(line.DepartmentCode, query.DepartmentCode))
            .Where(line => Blank(query.Status) || Same(line.Status, query.Status))
            .Where(line => string.IsNullOrEmpty(search) ||
                           line.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           line.EmployeeNumber.Contains(search, StringComparison.OrdinalIgnoreCase))
            .Select(line => new SalaryRegisterRowDto(
                line.EmployeeId.ToString(CultureInfo.InvariantCulture),
                line.EmployeeNumber,
                line.DisplayName,
                NameOr(divisionNames, line.DivisionCode, "Unassigned"),
                NameOr(departmentNames, line.DepartmentCode, "Unassigned"),
                line.Designation,
                line.GradeCode,
                line.PayableDays,
                line.DaysInMonth,
                new PayrollAmountsDto(
                    line.Basic, line.Hra, line.Conveyance, line.OtherAllowances, line.Gross,
                    line.Pf, line.Esi, line.Tds, line.OtherDeductions, line.Deductions, line.NetPayable),
                line.Status,
                line.PaidOn,
                line.PaymentMode))
            .OrderBy(row => row.DivisionName).ThenBy(row => row.DepartmentName).ThenBy(row => row.DisplayName)
            .ToList();

        return new SalaryRegisterDto(
            query.Month, query.Year, MonthLabel(first), true, run.ApprovedUtc is not null, run.ApprovedUtc,
            rows, PayrollAmountsDto.Sum(rows.Select(row => row.Amounts)));
    }

    // ---- Shared -------------------------------------------------------------

    private async Task<(IReadOnlyList<EmployeeRecord> Records, IReadOnlyList<EmployeeDirectoryRowDto> Rows)> LoadDirectoryAsync(
        string tenantKey, CancellationToken cancellationToken)
    {
        var people = await employees.QueryAsync(
            tenantKey, new EmployeeQueryFilter(null, null, null, null, null), cancellationToken);

        var options = await masters.GetAllAsync(tenantKey, cancellationToken);
        var divisionNames = NamesFor(options, MasterCategories.Division);
        var departmentNames = NamesFor(options, MasterCategories.Department);
        var locationNames = NamesFor(options, MasterCategories.Location);
        var gradeNames = NamesFor(options, MasterCategories.Grade);

        var byId = people.ToDictionary(person => person.Id);
        var directReports = people
            .Where(person => person.ManagerEmployeeId is not null)
            .GroupBy(person => person.ManagerEmployeeId!.Value)
            .ToDictionary(group => group.Key, group => group.Count());

        var rows = people
            .Select(person =>
            {
                var manager = person.ManagerEmployeeId is { } managerId ? byId.GetValueOrDefault(managerId) : null;
                var division = person.DivisionCode ?? string.Empty;

                return new EmployeeDirectoryRowDto(
                    person.Id.ToString(CultureInfo.InvariantCulture),
                    person.EmployeeNumber,
                    person.DisplayName,
                    person.JobTitle,
                    division,
                    NameOr(divisionNames, division, "Unassigned"),
                    person.DepartmentCode,
                    NameOr(departmentNames, person.DepartmentCode, "Unassigned"),
                    NameOr(locationNames, person.LocationCode, "—"),
                    person.GradeCode,
                    person.GradeCode is null ? null : NameOr(gradeNames, person.GradeCode, person.GradeCode),
                    person.JoinedOn,
                    EmploymentTypeLabel(person.EmploymentTypeCode),
                    person.EmploymentStatusCode,
                    person.IsActive,
                    person.Email,
                    person.Phone,
                    manager?.Id.ToString(CultureInfo.InvariantCulture),
                    manager?.DisplayName,
                    directReports.GetValueOrDefault(person.Id));
            })
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (people, rows);
    }

    private static bool Matches(EmployeeReportQuery query, EmployeeDirectoryRowDto row)
    {
        var search = query.Search?.Trim();

        return (Blank(query.EmployeeId) || Same(row.EmployeeId, query.EmployeeId)) &&
               (Blank(query.DivisionCode) || Same(row.DivisionCode, query.DivisionCode)) &&
               (Blank(query.DepartmentCode) || Same(row.DepartmentCode, query.DepartmentCode)) &&
               (Blank(query.Status) ||
                (Same(query.Status, "Active") && row.IsActive) ||
                (Same(query.Status, "Inactive") && !row.IsActive)) &&
               (string.IsNullOrEmpty(search) ||
                row.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                row.EmployeeNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (row.JobTitle?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (row.Email?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
    }

    private static (DateOnly First, DateOnly Last) MonthOf(EmployeeReportQuery query)
    {
        if (query.Month is < 1 or > 12 || query.Year is < 2000 or > 2100)
        {
            throw new MobileValidationException("SCOPE_INVALID", "Choose a valid month and year.");
        }

        var first = new DateOnly(query.Year, query.Month, 1);
        return (first, first.AddMonths(1).AddDays(-1));
    }

    private static string MonthLabel(DateOnly first) =>
        first.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    private static string EmploymentTypeLabel(string code) => code.ToUpperInvariant().Replace(' ', '_') switch
    {
        "FULL_TIME" => "Full time",
        "PART_TIME" => "Part time",
        "CONTRACT" => "Contract",
        "INTERN" => "Intern",
        _ => code
    };

    private static Dictionary<string, string> NamesFor(IReadOnlyList<MasterOptionRecord> options, string category) =>
        options
            .Where(option => option.Category == category)
            .GroupBy(option => option.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

    // Some older rows hold a name rather than a code; show it as it is.
    private static string NameOr(IReadOnlyDictionary<string, string> names, string? code, string fallback) =>
        string.IsNullOrWhiteSpace(code) ? fallback : names.GetValueOrDefault(code) ?? code;

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }
}
