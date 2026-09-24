using System.Globalization;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// The tiles on the employee Home and Attendance screens.
///
/// Counts are derived from the attendance days and leave rows at read time. Nothing here is
/// stored, so a tile cannot disagree with the record it summarises.
/// </summary>
public sealed class EmployeeKpiService(
    IEmployeeRepository employees,
    IAttendanceRepository attendance,
    ILeaveRepository leave,
    IMovementRepository movement,
    IInterviewRepository interviews) : IEmployeeKpiService
{
    public async Task<EmployeeHomeSummaryDto> GetHomeSummaryAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var from = new DateOnly(today.Year, today.Month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var days = await attendance.QueryDaysAsync(
            caller.TenantKey,
            new AttendanceDayFilter(from, to, employee.Id, null, null, false),
            ct);

        var presentDays = days.Count(day => day.Status is "Present" or "Partial");
        var leaveAvailable = await TotalLeaveAvailableAsync(caller, employee.Id, today.Year, ct);

        // Tile 3: everything the employee is still waiting on a decision for. Each source is the
        // real request table, so approving on the HR portal moves this number straight away.
        var openLeave = await leave.QueryRequestsAsync(
            caller.TenantKey, new LeaveRequestFilter(employee.Id, null, "Pending", null, null), ct);
        var openCorrections = await attendance.QueryCorrectionsAsync(
            caller.TenantKey, employee.Id, "Pending", ct);
        var openPasses = await movement.QueryPassesAsync(
            caller.TenantKey, new GatePassFilter(null, null, [employee.Id], "Pending", null), ct);
        var pendingRequests = openLeave.Count + openCorrections.Count + openPasses.Count;

        // Tile 4: gate passes asked for this month, whatever became of them — except the ones the
        // employee took back, which were never really used.
        var monthPasses = await movement.QueryPassesAsync(
            caller.TenantKey, new GatePassFilter(from, to, [employee.Id], null, null), ct);
        var gatePasses = monthPasses.Count(pass => pass.State is not ("Withdrawn" or "Cancelled"));

        // The four tiles of 5.png. Active Projects and Performance had no module behind them, so
        // their slots carry Pending Requests and Gate Passes (employee-app-build-plan.md D2).
        var kpis = new List<KpiDto>
        {
            new(EmployeeKpiKeys.PresentDays, "Present Days",
                presentDays.ToString(CultureInfo.InvariantCulture), null, null, "info",
                "/attendance"),

            new(EmployeeKpiKeys.LeaveBalance, "Leave Balance",
                Format(leaveAvailable), "days", null, "accent", "/leave"),

            new(EmployeeKpiKeys.PendingRequests, "Pending Requests",
                pendingRequests.ToString(CultureInfo.InvariantCulture), null, null,
                pendingRequests == 0 ? "success" : "warning", "/leave/history"),

            new(EmployeeKpiKeys.GatePasses, "Gate Passes",
                gatePasses.ToString(CultureInfo.InvariantCulture), null, null, "warning",
                "/menu/gate-passes")
        };

        var identity = new EmployeeIdentityCardDto(
            employee.Id.ToString(CultureInfo.InvariantCulture),
            employee.EmployeeNumber,
            $"{employee.LegalFirstName} {employee.LegalLastName}".Trim(),
            employee.JobTitle,
            employee.DepartmentCode,
            employee.Phone,
            employee.AvatarUrl,
            employee.EmployeeNumber);

        return new EmployeeHomeSummaryDto(identity, kpis, from, to, DateTimeOffset.UtcNow);
    }

    public async Task<AttendanceKpiSummaryDto> GetAttendanceKpisAsync(
        MobileCallerContext caller, AttendanceKpiQuery query, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var (year, month) = ResolveMonth(query.Month);

        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        var days = await attendance.QueryDaysAsync(
            caller.TenantKey,
            new AttendanceDayFilter(from, to, employee.Id, null, null, false),
            ct);

        // Holidays come from the calendar rather than from attendance rows: a holiday is a
        // holiday whether or not anyone recorded a day against it.
        var holidays = await leave.GetHolidaysAsync(
            caller.TenantKey, from, to, employee.LocationCode, ct);

        var present = days.Count(day => day.Status == "Present");
        var partial = days.Count(day => day.Status == "Partial");
        var absent = days.Count(day => day.Status == "Absent");

        var holidayDates = holidays.Select(item => item.Date).ToHashSet();
        var workingDays = CountWorkingDays(from, to, holidayDates);

        var kpis = new List<KpiDto>
        {
            new(EmployeeKpiKeys.PresentDays, "Present Days",
                present.ToString(CultureInfo.InvariantCulture), null, null, "success", null),
            new(EmployeeKpiKeys.AbsentDays, "Absent Days",
                absent.ToString(CultureInfo.InvariantCulture), null, null, "danger", null),
            new(EmployeeKpiKeys.HalfDays, "Half Days",
                partial.ToString(CultureInfo.InvariantCulture), null, null, "warning", null),
            new(EmployeeKpiKeys.Holidays, "Holidays",
                holidayDates.Count.ToString(CultureInfo.InvariantCulture), null, null, "info", null)
        };

        return new AttendanceKpiSummaryDto(
            year,
            month,
            CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(month),
            kpis,
            workingDays,
            DateTimeOffset.UtcNow);
    }

    public async Task<LeaveKpiSummaryDto> GetLeaveKpisAsync(
        MobileCallerContext caller, LeaveKpiQuery query, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var year = query.Year ?? DateTime.UtcNow.Year;

        var policies = await leave.GetPoliciesAsync(caller.TenantKey, true, ct);
        var balances = await leave.GetBalancesAsync(caller.TenantKey, employee.Id, year, ct);
        var consumption = await leave.GetConsumptionAsync(caller.TenantKey, employee.Id, year, ct);

        var kpis = new List<KpiDto>();

        foreach (var policy in policies)
        {
            var balance = balances.FirstOrDefault(item => item.PolicyId == policy.Id);
            var used = consumption.FirstOrDefault(item => item.PolicyId == policy.Id);

            var granted = (balance?.Accrued ?? 0m) + (balance?.CarriedForward ?? 0m) +
                          (balance?.AdjustmentDays ?? 0m);
            var available = granted - (used?.UsedDays ?? 0m) - (used?.PendingDays ?? 0m);

            kpis.Add(new KpiDto(
                $"leave-{policy.Code.ToLowerInvariant()}",
                policy.Name,
                Format(available),
                policy.Unit.ToLowerInvariant(),
                null,
                available <= 0 ? "muted" : "accent",
                "/leave/balances"));
        }

        return new LeaveKpiSummaryDto(year, kpis, DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// The Home Interview Schedule card (5.png). A panel member is identified by their account,
    /// so this needs no employee record — a recruiter without one still sees their panels.
    /// Upcoming interviews come first, soonest first; past ones follow, most recent first.
    /// </summary>
    public async Task<EmployeeInterviewScheduleDto> GetMyInterviewsAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTimeOffset.UtcNow;

        var rows = await interviews.QueryAsync(
            caller.TenantKey,
            new InterviewFilter(today.AddDays(-7), today.AddDays(14), null, caller.UserId, null),
            ct);

        var items = rows
            .Where(row => !string.Equals(row.Interview.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
            .OrderBy(row => row.Interview.EndUtc < now)
            .ThenBy(row => row.Interview.EndUtc < now ? -row.Interview.StartUtc.UtcTicks : row.Interview.StartUtc.UtcTicks)
            .Select(InterviewService.MapListItem)
            .ToList();

        // Someone who holds the interview permission is an interviewer even in a quiet fortnight:
        // the card stays, saying there is nothing scheduled, rather than appearing and vanishing.
        var isInterviewer = items.Count > 0 || caller.Has("RECRUITMENT.INTERVIEWS");

        return new EmployeeInterviewScheduleDto(isInterviewer, items, now);
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task<VO.Employee.EmployeeRecord> RequireOwnEmployeeAsync(
        MobileCallerContext caller, CancellationToken ct) =>
        await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct)
        ?? throw new MobileValidationException(
            "NO_EMPLOYEE_RECORD",
            "This account is not linked to an employee record, so it has nothing to summarise.");

    private async Task<decimal> TotalLeaveAvailableAsync(
        MobileCallerContext caller, long employeeId, int year, CancellationToken ct)
    {
        var balances = await leave.GetBalancesAsync(caller.TenantKey, employeeId, year, ct);
        var consumption = await leave.GetConsumptionAsync(caller.TenantKey, employeeId, year, ct);

        var granted = balances.Sum(item => item.Accrued + item.CarriedForward + item.AdjustmentDays);
        var spent = consumption.Sum(item => item.UsedDays + item.PendingDays);

        return granted - spent;
    }

    /// <summary>
    /// Parses the <c>yyyy-MM</c> the screen asked for. An unparseable value falls back to the
    /// current month rather than throwing, because the annotation on the query has already
    /// rejected anything malformed.
    /// </summary>
    private static (int Year, int Month) ResolveMonth(string? month)
    {
        var now = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(month) ||
            !DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
        {
            return (now.Year, now.Month);
        }

        return (parsed.Year, parsed.Month);
    }

    /// <summary>Weekdays that are not public holidays — the denominator behind the tiles.</summary>
    private static int CountWorkingDays(DateOnly from, DateOnly to, HashSet<DateOnly> holidays)
    {
        var count = 0;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            if (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday || holidays.Contains(date))
            {
                continue;
            }

            count++;
        }

        return count;
    }

    /// <summary>Trims a trailing <c>.0</c> so a whole number of days reads as "3", not "3.0".</summary>
    private static string Format(decimal value) =>
        value.ToString(value == Math.Truncate(value) ? "0" : "0.#", CultureInfo.InvariantCulture);
}
