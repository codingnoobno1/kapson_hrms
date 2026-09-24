using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.DL.ITF.Workforce;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Holidays and interviews on one timeline.
///
/// Each source keeps its own permission: holidays are company-wide and need none, while
/// interviews are only merged in for a caller who may open the schedule. Identifiers carry
/// their source as a prefix so a single event can be fetched back without a second lookup
/// against every table that might own it.
/// </summary>
public sealed class MobileCalendarService(
    ILeaveRepository leave,
    IInterviewRepository interviews) : IMobileCalendarService
{
    private const string HolidayPrefix = "holiday:";
    private const string InterviewPrefix = "interview:";
    private const string InterviewPermission = "RECRUITMENT.VIEW";

    public async Task<IReadOnlyList<CalendarEventDto>> GetRangeAsync(
        MobileCallerContext caller, CalendarRangeQuery query, CancellationToken ct)
    {
        if (query.To < query.From)
        {
            throw new MobileValidationException("RANGE_INVALID", "The end date falls before the start date.");
        }

        var events = new List<CalendarEventDto>();

        if (Wanted(query, "holiday"))
        {
            var holidays = await leave.GetHolidaysAsync(caller.TenantKey, query.From, query.To, null, ct);
            events.AddRange(holidays.Select(ToEvent));
        }

        if (Wanted(query, "interview") && caller.Has(InterviewPermission))
        {
            var scheduled = await interviews.QueryAsync(
                caller.TenantKey, new InterviewFilter(query.From, query.To, null, null, null), ct);

            events.AddRange(scheduled.Select(ToEvent));
        }

        return events.OrderBy(item => item.StartUtc).ThenBy(item => item.Title).ToList();
    }

    public async Task<CalendarEventDto> GetEventAsync(
        MobileCallerContext caller, string eventId, CancellationToken ct)
    {
        if (eventId.StartsWith(HolidayPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var id = Identifier(eventId, HolidayPrefix);

            // Holidays are addressed by date range rather than by key, so the surrounding year
            // is the narrowest read that is certain to contain the one asked for.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var holidays = await leave.GetHolidaysAsync(
                caller.TenantKey, today.AddYears(-1), today.AddYears(1), null, ct);

            var match = holidays.FirstOrDefault(item => item.Id == id)
                ?? throw new MobileNotFoundException("That holiday no longer exists.");

            return ToEvent(match);
        }

        if (eventId.StartsWith(InterviewPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (!caller.Has(InterviewPermission))
            {
                throw new MobileAccessDeniedException(InterviewPermission);
            }

            var id = Identifier(eventId, InterviewPrefix);
            var match = await interviews.GetAsync(caller.TenantKey, id, ct)
                ?? throw new MobileNotFoundException("That interview no longer exists.");

            return ToEvent(match);
        }

        throw new MobileNotFoundException("That event no longer exists.");
    }

    private static long Identifier(string eventId, string prefix) =>
        long.TryParse(eventId[prefix.Length..], out var id)
            ? id
            : throw new MobileNotFoundException("That event no longer exists.");

    private static CalendarEventDto ToEvent(VO.Workforce.LeaveHolidayRecord holiday)
    {
        var start = new DateTimeOffset(holiday.Date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return new CalendarEventDto(
            $"{HolidayPrefix}{holiday.Id}",
            "holiday",
            holiday.Name,
            start,
            start.AddDays(1),
            IsAllDay: true,
            Route: null);
    }

    private static CalendarEventDto ToEvent(VO.Recruitment.InterviewSummaryRecord row) => new(
        $"{InterviewPrefix}{row.Interview.Id}",
        "interview",
        $"{row.CandidateName} · {row.JobTitle}",
        row.Interview.StartUtc,
        row.Interview.StartUtc.AddMinutes(row.Interview.DurationMinutes),
        IsAllDay: false,
        Route: $"/interviews/{row.Interview.Id}");

    /// <summary>An empty type list means every source the caller can reach.</summary>
    private static bool Wanted(CalendarRangeQuery query, string type) =>
        query.EventTypes.Count == 0 ||
        query.EventTypes.Contains(type, StringComparer.OrdinalIgnoreCase);
}
