using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.BM.ITF.Workforce;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Employee;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Workforce;

/// <summary>
/// Turns the absence nobody recorded into the absence the register holds.
///
/// Attendance days are written when something happens: a punch, or an approved correction.
/// Not turning up is the one event that produces neither, so an absence existed only as an
/// inference on a dashboard and never as a row anyone could query, report on or dispute. This
/// closes a finished date by writing the rows those days should always have had.
///
/// It only ever fills gaps. A day that already has a row was written by something that knows
/// more than this job does, and is left exactly as it is.
/// </summary>
public sealed class AttendanceDayCloser(
    IEmployeeRepository employees,
    IAttendanceRepository attendance,
    ILeaveRepository leave,
    IShiftScheduleService schedule,
    ITenantClock clock) : IAttendanceDayCloser
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>The exception recorded on a day that was expected and never worked.</summary>
    private const string NoPunches = "NoPunches";

    public async Task<AttendanceCloseResultDto> CloseAsync(
        string tenantKey, DateOnly date, CancellationToken ct = default)
    {
        var today = await clock.TodayAsync(tenantKey, ct);

        if (date >= today)
        {
            throw new InvalidOperationException(
                $"{date:dd MMM yyyy} is not over yet for this client. Closing a day still being " +
                "worked would record absences against people who simply have not arrived.");
        }

        // Employed on that date. Somebody who joins next week was not absent last Tuesday, and
        // somebody who left is not absent every day since.
        var population = await employees.QueryAsync(
            tenantKey,
            new EmployeeQueryFilter(null, null, null, null, null,
                ActiveOnly: true, JoinedOnOrBefore: date),
            ct);

        var ids = population.Select(person => person.Id).ToList();

        if (ids.Count == 0)
        {
            return new AttendanceCloseResultDto(tenantKey, date, 0, 0, 0, 0, 0, false);
        }

        var holidays = await leave.GetHolidaysAsync(tenantKey, date, date, null, ct);
        var isHoliday = holidays.Count > 0;

        var existing = await attendance.QueryDaysAsync(
            tenantKey, new AttendanceDayFilter(date, date, null, ids, null, false), ct);

        var recorded = existing.Select(day => day.EmployeeId).ToHashSet();

        // A public holiday is nobody's absence. The register still gets a row, so a reader can
        // tell "the company was closed" from "we have not closed this day yet".
        var onLeave = isHoliday
            ? []
            : (await leave.GetOnLeaveAsync(tenantKey, date, ids, ct)).ToHashSet();

        var shifts = await schedule.ResolveAsync(tenantKey, ids, date, date, ct);
        var now = DateTimeOffset.UtcNow;

        var absent = 0;
        var leaveDays = 0;
        var restDays = 0;

        foreach (var person in population)
        {
            if (recorded.Contains(person.Id))
            {
                continue;
            }

            var expected = shifts.For(person.Id, date);
            var isRestDay = expected is { IsWorkingDay: false };

            // The order matters. A rest day is not leave, and leave is not absence: charging
            // somebody an absence for a day they had booked off, or for a Sunday, is the
            // complaint that makes a workforce stop trusting the register entirely.
            var (status, exceptions) =
                isHoliday || isRestDay ? ("WeeklyOff", Array.Empty<string>())
                : onLeave.Contains(person.Id) ? ("Leave", [])
                : ("Absent", new[] { NoPunches });

            await attendance.UpsertDayAsync(
                new AttendanceDayRecord(
                    0, tenantKey, person.Id, date,
                    expected?.ShiftCode,
                    status,
                    0,
                    status == "Absent" ? expected?.ExpectedMinutes ?? 0 : 0,
                    JsonSerializer.Serialize(exceptions, Json),
                    now),
                ct);

            switch (status)
            {
                case "Absent": absent++; break;
                case "Leave": leaveDays++; break;
                default: restDays++; break;
            }
        }

        return new AttendanceCloseResultDto(
            tenantKey, date, ids.Count, recorded.Count, absent, leaveDays, restDays, isHoliday);
    }

    public async Task<IReadOnlyList<AttendanceCloseResultDto>> CatchUpAsync(
        string tenantKey, int lookbackDays, CancellationToken ct = default)
    {
        var today = await clock.TodayAsync(tenantKey, ct);
        var window = Math.Clamp(lookbackDays, 1, 90);

        var results = new List<AttendanceCloseResultDto>();

        // Oldest first, so a day that fails does not leave a hole behind newer closed days.
        for (var offset = window; offset >= 1; offset--)
        {
            var date = today.AddDays(-offset);

            var result = await CloseAsync(tenantKey, date, ct);

            // Only report the days that actually changed something. A quiet run over a month
            // of already-closed days should not produce a month of log lines.
            if (result.MarkedAbsent + result.MarkedOnLeave + result.MarkedRestDay > 0)
            {
                results.Add(result);
            }
        }

        return results;
    }
}
