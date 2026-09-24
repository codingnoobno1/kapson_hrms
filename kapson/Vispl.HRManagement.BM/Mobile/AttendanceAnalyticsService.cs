using System.Globalization;
using System.Net;
using System.Text;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Employee;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Attendance Analytics (phase C). Read-time aggregation over the same attendance days, holiday
/// calendar and shift resolver as the tiles and the HR portal reports, so an analytics figure
/// cannot disagree with the Attendance tab or with <c>/hr/reports/attendance-monthly</c>.
///
/// Attendance rate = (Present + ½ × Half Days) ÷ working days, where working days are the
/// weekdays since joining, less holidays and approved leave (decision Q4, 2026-09-16).
/// </summary>
public sealed class AttendanceAnalyticsService(
    IEmployeeRepository employees,
    IAttendanceRepository attendance,
    ILeaveRepository leave,
    IShiftScheduleService schedule,
    ITenantClock clock,
    ITF.Admin.IEmployeeAppSettingsService appSettings,
    IExcelExporter excel,
    IEmailSender email) : IAttendanceAnalyticsService
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public async Task<AttendanceAnalyticsDto> GetAnalyticsAsync(
        MobileCallerContext caller, AttendanceAnalyticsQuery query, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        return await ComputeAsync(caller.TenantKey, employee, query, ct);
    }

    public async Task<AttendanceReportFileDto> BuildReportAsync(
        MobileCallerContext caller, AttendanceAnalyticsQuery query, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var analytics = await ComputeAsync(caller.TenantKey, employee, query, ct);
        var period = analytics.Period;

        var days = await attendance.QueryDaysAsync(
            caller.TenantKey, new AttendanceDayFilter(period.From, period.To, employee.Id, null, null, false), ct);
        var punches = (await attendance.GetPunchesAsync(caller.TenantKey, employee.Id, period.From, period.To, ct))
            .GroupBy(item => item.WorkDate)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<AttendancePunchRecord>)group.ToList());
        var zone = await clock.ResolveZoneAsync(caller.TenantKey, ct);
        var shifts = await schedule.ResolveAsync(caller.TenantKey, [employee.Id], period.From, period.To, ct);

        var subtitle = $"{employee.DisplayName} ({employee.EmployeeNumber}) · {period.Label}: {period.From:dd MMM yyyy} – {period.To:dd MMM yyyy}";
        var stats = analytics.Statistics;

        var summary = new ExcelSheet("Summary", "Attendance analytics", subtitle,
            [new("Measure"), new("Value", ExcelFormat.Decimal)],
            [
                ["Working days", stats.WorkingDays],
                ["Days present", stats.Present],
                ["Days absent", stats.Absent],
                ["Half days", stats.HalfDays],
                ["Attendance rate %", stats.AttendanceRate],
                ["Target %", analytics.TargetPercent]
            ]);

        var months = new ExcelSheet("Months", "Monthly breakdown", subtitle,
            [
                new("Month"), new("Attendance %", ExcelFormat.Decimal), new("Present", ExcelFormat.Integer),
                new("Absent", ExcelFormat.Integer), new("Half Days", ExcelFormat.Integer)
            ],
            analytics.Months
                .Select(month => (IReadOnlyList<object?>)[$"{month.Label} {month.Year}", month.AttendancePercent, month.Present, month.Absent, month.HalfDays])
                .ToList());

        var dayRows = days
            .OrderBy(day => day.WorkDate)
            .Select(day =>
            {
                var mapped = AttendanceService.MapDayWithBadge(day, punches.GetValueOrDefault(day.WorkDate, []), shifts, zone);
                return (IReadOnlyList<object?>)
                [
                    day.WorkDate, day.Status, mapped.Badge?.Name, mapped.CheckIn?.ToString("h:mm tt", Invariant),
                    mapped.CheckOut?.ToString("h:mm tt", Invariant), Math.Round(day.WorkedMinutes / 60m, 2)
                ];
            })
            .ToList();

        var daySheet = new ExcelSheet("Days", "Daily attendance", subtitle,
            [
                new("Date", ExcelFormat.Date), new("Status"), new("Remark"), new("Check-in"), new("Check-out"),
                new("Worked hours", ExcelFormat.Decimal)
            ],
            dayRows);

        var bytes = excel.Build(summary, months, daySheet);

        return new AttendanceReportFileDto(
            $"attendance-{period.Key}-{period.To:yyyy-MM-dd}.xlsx",
            ExcelContentTypes.Xlsx,
            Convert.ToBase64String(bytes));
    }

    public async Task<AttendanceReportEmailDto> EmailReportAsync(
        MobileCallerContext caller, AttendanceAnalyticsQuery query, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);

        if (string.IsNullOrWhiteSpace(employee.Email))
        {
            return new AttendanceReportEmailDto(false, null, "There is no email address on your employee record. Ask HR to add one.");
        }

        var analytics = await ComputeAsync(caller.TenantKey, employee, query, ct);
        var (text, html) = RenderEmail(employee, analytics);

        var result = await email.SendAsync(new EmailMessage(
            employee.Email,
            employee.DisplayName,
            $"Your attendance analytics — {analytics.Period.Label}",
            text,
            html), ct);

        return new AttendanceReportEmailDto(result.Sent, employee.Email, result.Sent ? null : result.Failure);
    }

    // ---- Computation ------------------------------------------------------

    private sealed record Tally(int WorkingDays, int Present, int Absent, int HalfDays)
    {
        public decimal Rate => WorkingDays == 0 ? 0 : Math.Round((Present + HalfDays * 0.5m) / WorkingDays * 100m, 1);
    }

    private async Task<AttendanceAnalyticsDto> ComputeAsync(
        string tenantKey, EmployeeRecord employee, AttendanceAnalyticsQuery query, CancellationToken ct)
    {
        var today = await clock.TodayAsync(tenantKey, ct);
        var settings = await appSettings.GetAsync(tenantKey, ct);
        var target = Math.Clamp(settings.AttendanceTargetPercent, 50, 100);

        var window = ResolveWindow(query, today);
        var current = await TallyAsync(tenantKey, employee, window.From, window.To, ct);
        var previous = await TallyAsync(tenantKey, employee, window.PreviousFrom, window.PreviousTo, ct);

        var kpis = new List<AnalyticsKpiDto>
        {
            new("overall-attendance", "Overall Attendance %", $"{current.Rate.ToString("0.#", Invariant)}%",
                Delta(current.Rate - previous.Rate, "%", higherIsBetter: true, window.Comparison)),
            new("days-present", "Days Present", current.Present.ToString(Invariant),
                Delta(current.Present - previous.Present, " days", higherIsBetter: true, window.Comparison)),
            new("days-absent", "Days Absent", current.Absent.ToString(Invariant),
                Delta(current.Absent - previous.Absent, " days", higherIsBetter: false, window.Comparison)),
            new("half-days", "Half Days", current.HalfDays.ToString(Invariant),
                Delta(current.HalfDays - previous.HalfDays, " days", higherIsBetter: false, window.Comparison))
        };

        // Monthly breakdown: at least the last three months, so "This Month" still shows a trend.
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthCount = Math.Max(3, window.MonthCount);
        var months = new List<AnalyticsMonthDto>();

        for (var offset = monthCount - 1; offset >= 0; offset--)
        {
            var start = monthStart.AddMonths(-offset);
            var end = Min(start.AddMonths(1).AddDays(-1), today);
            var tally = await TallyAsync(tenantKey, employee, start, end, ct);
            months.Add(new AnalyticsMonthDto(start.Year, start.Month,
                Invariant.DateTimeFormat.GetAbbreviatedMonthName(start.Month), tally.Rate, tally.Present, tally.Absent, tally.HalfDays));
        }

        // Weekly breakdown for the current month, in seven-day blocks.
        var weeks = new List<AnalyticsWeekDto>();
        for (var (start, index) = (monthStart, 1); start <= today; start = start.AddDays(7), index++)
        {
            var end = Min(start.AddDays(6), today);
            var tally = await TallyAsync(tenantKey, employee, start, end, ct);
            if (tally.WorkingDays > 0)
            {
                weeks.Add(new AnalyticsWeekDto($"Week {index}", Math.Round(tally.Rate, 0)));
            }
        }

        var insights = BuildInsights(current, previous, target, weeks, window.Comparison);

        return new AttendanceAnalyticsDto(
            new AnalyticsPeriodDto(window.Key, window.Label, window.From, window.To, window.Comparison),
            kpis,
            months,
            weeks,
            insights,
            new AnalyticsStatisticsDto(current.WorkingDays, current.Present, current.Absent, current.HalfDays, current.Rate),
            target,
            DateTimeOffset.UtcNow);
    }

    private async Task<Tally> TallyAsync(
        string tenantKey, EmployeeRecord employee, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var start = from < employee.JoinedOn ? employee.JoinedOn : from;
        if (to < start)
        {
            return new Tally(0, 0, 0, 0);
        }

        var days = await attendance.QueryDaysAsync(
            tenantKey, new AttendanceDayFilter(start, to, employee.Id, null, null, false), ct);
        var holidays = (await leave.GetHolidaysAsync(tenantKey, start, to, employee.LocationCode, ct))
            .Select(item => item.Date)
            .ToHashSet();
        var leaveDates = days.Where(day => Has(day.Status, "leave")).Select(day => day.WorkDate).ToHashSet();

        var working = 0;
        for (var date = start; date <= to; date = date.AddDays(1))
        {
            if (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) &&
                !holidays.Contains(date) && !leaveDates.Contains(date))
            {
                working++;
            }
        }

        return new Tally(
            working,
            days.Count(day => Has(day.Status, "present")),
            days.Count(day => Has(day.Status, "absent")),
            days.Count(day => Has(day.Status, "partial") || Has(day.Status, "half")));
    }

    private static List<AnalyticsInsightDto> BuildInsights(
        Tally current, Tally previous, int target, IReadOnlyList<AnalyticsWeekDto> weeks, string comparison)
    {
        var insights = new List<AnalyticsInsightDto>();
        var presentChange = current.Present - previous.Present;
        var absentChange = current.Absent - previous.Absent;
        var against = comparison.Replace("vs ", "compared to ", StringComparison.Ordinal).ToLowerInvariant();

        insights.Add(presentChange switch
        {
            > 0 => new("trend", "📈", "Positive Trend", $"Present days increased by {presentChange} {DayWord(presentChange)} {against}."),
            < 0 => new("trend", "📉", "Attendance Dip", $"Present days fell by {-presentChange} {DayWord(-presentChange)} {against}."),
            _ => new("trend", "➡️", "Steady Attendance", $"Present days are the same {against}.")
        });

        insights.Add(absentChange > 0
            ? new("watch-out", "⚠️", "Watch Out", $"Absent days increased by {absentChange}. Try to maintain consistency to meet the {target}% target.")
            : new("watch-out", "✅", "No New Absences", absentChange < 0
                ? $"Absent days fell by {-absentChange}. Keep it up."
                : "Absent days did not increase."));

        var gap = Math.Round(current.Rate - target, 1);
        insights.Add(new("target", "🎯", "Target Status", gap >= 0
            ? $"Currently {gap.ToString("0.#", Invariant)}% above the {target}% attendance target."
            : $"Currently {(-gap).ToString("0.#", Invariant)}% below the {target}% attendance target."));

        if (weeks.Count > 0)
        {
            insights.Add(new("weekly", "📊", "Weekly Breakdown",
                string.Join(" | ", weeks.Select(week => $"{week.Label}: {week.AttendancePercent.ToString("0", Invariant)}%"))));
        }

        return insights;
    }

    private sealed record Window(
        string Key, string Label, string Comparison, DateOnly From, DateOnly To, DateOnly PreviousFrom, DateOnly PreviousTo, int MonthCount);

    private static Window ResolveWindow(AttendanceAnalyticsQuery query, DateOnly today)
    {
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        // A specific calendar month — the Full Month Attendance page's stepper — wins over a
        // rolling Period window when both are supplied.
        if (!string.IsNullOrWhiteSpace(query.Month) &&
            DateTime.TryParseExact(query.Month, "yyyy-MM", Invariant, DateTimeStyles.None, out var parsedMonth))
        {
            var from = new DateOnly(parsedMonth.Year, parsedMonth.Month, 1);
            var to = Min(from.AddMonths(1).AddDays(-1), today);
            var label = $"{Invariant.DateTimeFormat.GetMonthName(parsedMonth.Month)} {parsedMonth.Year}";
            var previousFrom = from.AddMonths(-1);
            var previousTo = previousFrom.AddMonths(1).AddDays(-1);

            return new(
                $"month-{parsedMonth.Year:0000}-{parsedMonth.Month:00}", label, "vs Previous Month",
                from, to, previousFrom, previousTo, 1);
        }

        switch (query.Period)
        {
            case AttendancePeriods.Last3Months:
            {
                var from = monthStart.AddMonths(-2);
                return new(query.Period, "Last 3 Months", "vs Previous 3 Months", from, today, from.AddMonths(-3), from.AddDays(-1), 3);
            }
            case AttendancePeriods.Last6Months:
            {
                var from = monthStart.AddMonths(-5);
                return new(query.Period, "Last 6 Months", "vs Previous 6 Months", from, today, from.AddMonths(-6), from.AddDays(-1), 6);
            }
            case AttendancePeriods.ThisYear:
            {
                var from = new DateOnly(today.Year, 1, 1);
                return new(query.Period, "This Year", "vs Last Year", from, today, from.AddYears(-1), today.AddYears(-1), today.Month);
            }
            default:
            {
                // Same number of days into last month, so a half-finished month is not compared
                // against a whole one.
                var previousFrom = monthStart.AddMonths(-1);
                var previousTo = Min(previousFrom.AddDays(today.Day - 1), monthStart.AddDays(-1));
                return new(AttendancePeriods.ThisMonth, "This Month", "vs Last Month", monthStart, today, previousFrom, previousTo, 1);
            }
        }
    }

    internal static AnalyticsDeltaDto Delta(decimal change, string unit, bool higherIsBetter, string comparison)
    {
        if (change == 0)
        {
            return new AnalyticsDeltaDto($"→ Same {comparison}", "same", "good");
        }

        var up = change > 0;
        var good = up == higherIsBetter;
        var magnitude = Math.Abs(change).ToString("0.#", Invariant);

        return new AnalyticsDeltaDto($"{(up ? "↑" : "↓")} {magnitude}{unit} {comparison}", up ? "up" : "down", good ? "good" : "bad");
    }

    private static (string Text, string Html) RenderEmail(EmployeeRecord employee, AttendanceAnalyticsDto analytics)
    {
        var stats = analytics.Statistics;
        var text = new StringBuilder()
            .AppendLine($"Hello {employee.DisplayName},")
            .AppendLine()
            .AppendLine($"Your attendance for {analytics.Period.Label} ({analytics.Period.From:dd MMM yyyy} – {analytics.Period.To:dd MMM yyyy}):")
            .AppendLine($"Attendance rate: {stats.AttendanceRate:0.#}% (target {analytics.TargetPercent}%)")
            .AppendLine($"Working days: {stats.WorkingDays} · Present: {stats.Present} · Absent: {stats.Absent} · Half days: {stats.HalfDays}")
            .AppendLine();

        foreach (var insight in analytics.Insights)
        {
            text.AppendLine($"{insight.Title}: {insight.Text}");
        }

        string Cell(string value, string align = "left") =>
            $"<td style=\"padding:8px 12px;border-bottom:1px solid #eee;text-align:{align}\">{WebUtility.HtmlEncode(value)}</td>";

        var html = new StringBuilder()
            .Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;color:#111827\">")
            .Append($"<p>Hello {WebUtility.HtmlEncode(employee.DisplayName)},</p>")
            .Append($"<p>Your attendance for <strong>{WebUtility.HtmlEncode(analytics.Period.Label)}</strong> ")
            .Append($"({analytics.Period.From:dd MMM yyyy} – {analytics.Period.To:dd MMM yyyy}).</p>")
            .Append("<table style=\"border-collapse:collapse;background:#F4F4F4;border-radius:8px\">")
            .Append($"<tr>{Cell("Total Working Days")}{Cell(stats.WorkingDays.ToString(Invariant), "right")}</tr>")
            .Append($"<tr>{Cell("Days Present")}{Cell(stats.Present.ToString(Invariant), "right")}</tr>")
            .Append($"<tr>{Cell("Days Absent")}{Cell(stats.Absent.ToString(Invariant), "right")}</tr>")
            .Append($"<tr>{Cell("Half Days")}{Cell(stats.HalfDays.ToString(Invariant), "right")}</tr>")
            .Append($"<tr>{Cell("Attendance Rate")}{Cell($"{stats.AttendanceRate.ToString("0.#", Invariant)}%", "right")}</tr>")
            .Append("</table>")
            .Append("<h3 style=\"margin-top:20px\">Key insights</h3><ul>");

        foreach (var insight in analytics.Insights)
        {
            html.Append($"<li><strong>{WebUtility.HtmlEncode(insight.Title)}</strong> — {WebUtility.HtmlEncode(insight.Text)}</li>");
        }

        html.Append("</ul><p style=\"color:#6B7280\">Sent from the Kapsons employee app.</p></div>");
        return (text.ToString(), html.ToString());
    }

    private async Task<EmployeeRecord> RequireOwnEmployeeAsync(MobileCallerContext caller, CancellationToken ct) =>
        await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct)
        ?? throw new MobileValidationException(
            "NO_EMPLOYEE_RECORD", "This account is not linked to an employee record, so it has no attendance.");

    private static bool Has(string status, string fragment) =>
        status.Contains(fragment, StringComparison.OrdinalIgnoreCase);

    private static DateOnly Min(DateOnly first, DateOnly second) => first < second ? first : second;

    private static string DayWord(int days) => days == 1 ? "day" : "days";
}
