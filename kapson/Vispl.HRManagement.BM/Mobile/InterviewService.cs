using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Recruitment;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.VO.Recruitment;

namespace Vispl.HRManagement.BM.Mobile;

public sealed class InterviewService(
    IInterviewRepository interviews,
    IApplicationRepository applications,
    ITF.Shared.INotificationPublisher notifications) : IInterviewService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string ViewPermission = "RECRUITMENT.INTERVIEWS";
    private const string ManagePermission = "RECRUITMENT.INTERVIEWS_MANAGE";

    /// <summary>Working hours slots are offered in, in the requested zone.</summary>
    private static readonly TimeOnly WorkingDayStart = new(9, 0);
    private static readonly TimeOnly WorkingDayEnd = new(18, 0);
    private const int SlotStepMinutes = 30;

    // ---- Reads ------------------------------------------------------------

    public async Task<CursorPage<InterviewListItemDto>> ListAsync(
        MobileCallerContext caller, InterviewListQuery query, CancellationToken ct)
    {
        Require(caller, ViewPermission);

        var filter = new InterviewFilter(
            query.From,
            query.To,
            query.Status,
            // "Assigned to me" means on the panel, which is the only sense in which an
            // interview belongs to someone.
            query.AssignedToMeOnly ? caller.UserId : null,
            null);

        var all = await interviews.QueryAsync(caller.TenantKey, filter, ct);

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            all = all
                .Where(row =>
                    row.CandidateName.Contains(query.Query, StringComparison.OrdinalIgnoreCase) ||
                    row.JobTitle.Contains(query.Query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var offset = DecodeCursor(query.Cursor);
        var page = all.Skip(offset).Take(pageSize).Select(MapListItem).ToList();
        var next = offset + page.Count;
        var hasMore = next < all.Count;

        return new CursorPage<InterviewListItemDto>(
            page, hasMore ? EncodeCursor(next) : null, hasMore, pageSize, all.Count);
    }

    public async Task<InterviewDetailDto> GetAsync(
        MobileCallerContext caller, string interviewId, CancellationToken ct)
    {
        Require(caller, ViewPermission);

        var row = await LoadAsync(caller, interviewId, ct);
        var panel = await interviews.GetPanelAsync(caller.TenantKey, row.Interview.Id, ct);

        return new InterviewDetailDto(
            MapListItem(row),
            panel.Select(member => new PersonReferenceDto(member.PanelMemberId, member.DisplayName)).ToList(),
            // An online interview has a link rather than a place; the join URL is only
            // released to someone on the panel.
            IsOnline(row.Interview) ? null : row.Interview.LocationOrLink,
            IsOnline(row.Interview) && panel.Any(member => member.PanelMemberId == caller.UserId)
                ? row.Interview.LocationOrLink
                : null,
            null);
    }

    public async Task<InterviewAvailabilityDto> GetAvailabilityAsync(
        MobileCallerContext caller, InterviewAvailabilityQuery query, CancellationToken ct)
    {
        Require(caller, ManagePermission);

        // Without a panel there is nothing to check against, and returning every slot as
        // free would invite a double booking. Refusing is the safe answer.
        if (query.PanelMemberIds.Count == 0)
        {
            throw new MobileValidationException(
                "PANEL_REQUIRED", "Name the panel members whose availability should be checked.");
        }

        if (query.To < query.From)
        {
            throw new MobileValidationException("RANGE_INVALID", "The end of the range is before its start.");
        }

        if (query.To.DayNumber - query.From.DayNumber > 30)
        {
            throw new MobileValidationException("RANGE_TOO_WIDE", "Search a window of 30 days or less.");
        }

        var zone = ResolveTimeZone(query.TimeZoneId);
        var fromUtc = ToUtc(query.From, WorkingDayStart, zone);
        var toUtc = ToUtc(query.To, WorkingDayEnd, zone);

        var bookings = await interviews.GetPanelBookingsAsync(
            caller.TenantKey, query.PanelMemberIds, fromUtc, toUtc, ct);

        var panel = await BuildPanelLookupAsync(caller.TenantKey, bookings, ct);
        var slots = new List<InterviewSlotDto>();

        for (var day = query.From; day <= query.To; day = day.AddDays(1))
        {
            // Weekends are not offered. A tenant working calendar would replace this.
            var weekday = day.DayOfWeek;
            if (weekday is DayOfWeek.Saturday or DayOfWeek.Sunday)
            {
                continue;
            }

            for (var start = WorkingDayStart;
                 start.AddMinutes(query.DurationMinutes) <= WorkingDayEnd;
                 start = start.AddMinutes(SlotStepMinutes))
            {
                var slotStart = ToUtc(day, start, zone);
                var slotEnd = slotStart.AddMinutes(query.DurationMinutes);

                // A slot in the past is never offered.
                if (slotStart <= DateTimeOffset.UtcNow)
                {
                    continue;
                }

                var busy = bookings
                    .Where(booking => booking.Overlaps(slotStart, slotEnd))
                    .SelectMany(booking => panel.GetValueOrDefault(booking.Id, []))
                    .Where(query.PanelMemberIds.Contains)
                    .Distinct()
                    .ToList();

                slots.Add(new InterviewSlotDto(
                    slotStart, slotEnd, query.TimeZoneId, busy.Count == 0, busy));
            }
        }

        return new InterviewAvailabilityDto(
            new DateRangeDto(query.From, query.To, query.TimeZoneId), slots);
    }

    public async Task<InterviewEvaluationDto> GetEvaluationAsync(
        MobileCallerContext caller, string interviewId, CancellationToken ct)
    {
        Require(caller, ViewPermission);

        var row = await LoadAsync(caller, interviewId, ct);
        var evaluation = await interviews.GetEvaluationAsync(
            caller.TenantKey, row.Interview.Id, caller.UserId, ct);

        var scores = evaluation is null
            ? []
            : JsonSerializer.Deserialize<List<ScreeningCriterionDto>>(evaluation.ScoresJson, Json) ?? [];

        return new InterviewEvaluationDto(
            interviewId,
            scores,
            evaluation?.Recommendation,
            evaluation?.Notes,
            evaluation?.State ?? "Draft",
            null);
    }

    // ---- Writes -----------------------------------------------------------

    public async Task<MutationResultDto> ScheduleAsync(
        MobileCallerContext caller, ScheduleInterviewRequest request, CancellationToken ct)
    {
        Require(caller, ManagePermission);

        var applicationId = ParseRequiredId(request.ApplicationId);
        var application = await applications.GetAsync(caller.TenantKey, applicationId, ct)
            ?? throw new MobileNotFoundException("That application no longer exists.");

        // Interviewing a withdrawn or rejected applicant is a mistake, not a workflow.
        if (application.Stage is ApplicationStages.Rejected or ApplicationStages.Withdrawn or ApplicationStages.Hired)
        {
            throw new MobileValidationException(
                "APPLICATION_CLOSED", $"This application is {application.Stage} and cannot be scheduled.");
        }

        if (request.StartUtc <= DateTimeOffset.UtcNow)
        {
            throw new MobileValidationException("START_IN_PAST", "An interview cannot be scheduled in the past.");
        }

        if (request.PanelMemberIds.Count == 0)
        {
            throw new MobileValidationException("PANEL_REQUIRED", "An interview needs at least one panel member.");
        }

        if (string.Equals(request.Mode, "Online", StringComparison.OrdinalIgnoreCase) &&
            string.IsNullOrWhiteSpace(request.LocationOrLink))
        {
            throw new MobileValidationException("LINK_REQUIRED", "An online interview needs a joining link.");
        }

        await EnsurePanelIsFreeAsync(
            caller.TenantKey, request.PanelMemberIds, request.StartUtc,
            request.StartUtc.AddMinutes(request.DurationMinutes), null, ct);

        var now = DateTimeOffset.UtcNow;

        var id = await interviews.UpsertAsync(
            new InterviewRecord(
                0, caller.TenantKey, applicationId, application.CandidateId, application.JobOpeningId,
                request.RoundCode, BuildRoundLabel(request.RoundCode), request.StartUtc,
                request.DurationMinutes, request.TimeZoneId, request.Mode, request.LocationOrLink,
                request.Instructions, "Scheduled", null, null, caller.UserId, now, now),
            ct);

        await interviews.ReplacePanelAsync(caller.TenantKey, id, BuildPanel(caller, request.PanelMemberIds, now), ct);

        var candidate = await applications.GetCandidateByIdAsync(caller.TenantKey, application.CandidateId, ct);
        var local = TimeZoneInfo.ConvertTime(request.StartUtc, ResolveTimeZone(request.TimeZoneId));

        // The panel is told, because they are the people who have to turn up.
        await notifications.PublishAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                "Interview",
                "Interview scheduled",
                $"{candidate?.FullName ?? "A candidate"} — {BuildRoundLabel(request.RoundCode)} on " +
                $"{local:d MMM} at {local:HH:mm}.",
                "/interviews",
                "Normal",
                "Interview",
                id.ToString()),
            request.PanelMemberIds,
            ct);

        return new MutationResultDto(id.ToString(), "Scheduled", null, now);
    }

    public async Task<MutationResultDto> RescheduleAsync(
        MobileCallerContext caller, string interviewId, RescheduleInterviewRequest request, CancellationToken ct)
    {
        Require(caller, ManagePermission);

        var row = await LoadAsync(caller, interviewId, ct);
        var interview = row.Interview;

        if (!string.Equals(interview.Status, "Scheduled", StringComparison.OrdinalIgnoreCase))
        {
            throw new MobileValidationException(
                "NOT_SCHEDULED", $"A {interview.Status.ToLowerInvariant()} interview cannot be rescheduled.");
        }

        if (request.StartUtc <= DateTimeOffset.UtcNow)
        {
            throw new MobileValidationException("START_IN_PAST", "An interview cannot be moved into the past.");
        }

        var panel = await interviews.GetPanelAsync(caller.TenantKey, interview.Id, ct);

        // The interview being moved must not clash with itself.
        await EnsurePanelIsFreeAsync(
            caller.TenantKey,
            panel.Select(member => member.PanelMemberId).ToList(),
            request.StartUtc,
            request.StartUtc.AddMinutes(request.DurationMinutes),
            interview.Id,
            ct);

        var now = DateTimeOffset.UtcNow;

        await interviews.UpsertAsync(
            interview with
            {
                StartUtc = request.StartUtc,
                DurationMinutes = request.DurationMinutes,
                TimeZoneId = request.TimeZoneId,
                UpdatedUtc = now
            },
            ct);

        return new MutationResultDto(interviewId, "Scheduled", null, now, "Interview rescheduled.");
    }

    public async Task<MutationResultDto> CancelAsync(
        MobileCallerContext caller, string interviewId, CancelInterviewRequest request, CancellationToken ct)
    {
        Require(caller, ManagePermission);

        var row = await LoadAsync(caller, interviewId, ct);
        var interview = row.Interview;

        if (string.Equals(interview.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            throw new MobileValidationException("ALREADY_CANCELLED", "This interview is already cancelled.");
        }

        if (string.IsNullOrWhiteSpace(request.ReasonCode))
        {
            throw new MobileValidationException("REASON_REQUIRED", "A reason is required to cancel an interview.");
        }

        var now = DateTimeOffset.UtcNow;

        await interviews.UpsertAsync(
            interview with
            {
                Status = "Cancelled",
                CancellationReasonCode = request.ReasonCode,
                CancellationComment = request.Comment,
                UpdatedUtc = now
            },
            ct);

        return new MutationResultDto(interviewId, "Cancelled", null, now);
    }

    public async Task<MutationResultDto> SaveEvaluationAsync(
        MobileCallerContext caller, string interviewId, SaveInterviewEvaluationRequest request, CancellationToken ct)
    {
        Require(caller, ViewPermission);

        var row = await LoadAsync(caller, interviewId, ct);
        var panel = await interviews.GetPanelAsync(caller.TenantKey, row.Interview.Id, ct);

        // Only someone on the panel evaluates, and only for themselves.
        if (panel.All(member => member.PanelMemberId != caller.UserId))
        {
            throw new MobileAccessDeniedException("interview panel membership");
        }

        var existing = await interviews.GetEvaluationAsync(caller.TenantKey, row.Interview.Id, caller.UserId, ct);

        if (existing is not null && string.Equals(existing.State, "Submitted", StringComparison.OrdinalIgnoreCase))
        {
            throw new MobileValidationException(
                "ALREADY_SUBMITTED", "A submitted evaluation cannot be changed.");
        }

        if (request.Submit && string.IsNullOrWhiteSpace(request.Recommendation))
        {
            throw new MobileValidationException(
                "RECOMMENDATION_REQUIRED", "A recommendation is required before submitting.");
        }

        var now = DateTimeOffset.UtcNow;

        await interviews.UpsertEvaluationAsync(
            new InterviewEvaluationRecord(
                existing?.Id ?? 0, caller.TenantKey, row.Interview.Id, caller.UserId,
                JsonSerializer.Serialize(request.Scores, Json),
                request.Recommendation, request.Notes,
                request.Submit ? "Submitted" : "Draft",
                request.Submit ? now : existing?.SubmittedUtc,
                now),
            ct);

        return new MutationResultDto(
            interviewId, request.Submit ? "Submitted" : "Draft", null, now);
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task EnsurePanelIsFreeAsync(
        string tenantKey,
        IReadOnlyList<string> panelMemberIds,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        long? ignoreInterviewId,
        CancellationToken ct)
    {
        var bookings = await interviews.GetPanelBookingsAsync(
            tenantKey, panelMemberIds, startUtc.AddHours(-12), endUtc.AddHours(12), ct);

        var clash = bookings.FirstOrDefault(booking =>
            booking.Id != ignoreInterviewId && booking.Overlaps(startUtc, endUtc));

        if (clash is not null)
        {
            throw new MobileValidationException(
                "PANEL_DOUBLE_BOOKED",
                $"A panel member is already interviewing at {clash.StartUtc:HH:mm} on {clash.StartUtc:d MMM}.");
        }
    }

    private async Task<Dictionary<long, List<string>>> BuildPanelLookupAsync(
        string tenantKey, IReadOnlyList<InterviewRecord> bookings, CancellationToken ct)
    {
        var lookup = new Dictionary<long, List<string>>();

        foreach (var booking in bookings)
        {
            var panel = await interviews.GetPanelAsync(tenantKey, booking.Id, ct);
            lookup[booking.Id] = panel.Select(member => member.PanelMemberId).ToList();
        }

        return lookup;
    }

    private async Task<InterviewSummaryRecord> LoadAsync(
        MobileCallerContext caller, string interviewId, CancellationToken ct) =>
        await interviews.GetAsync(caller.TenantKey, ParseRequiredId(interviewId), ct)
        ?? throw new MobileNotFoundException("That interview no longer exists.");

    private static List<InterviewPanelistRecord> BuildPanel(
        MobileCallerContext caller, IReadOnlyList<string> memberIds, DateTimeOffset now) =>
        memberIds
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select((memberId, index) => new InterviewPanelistRecord(
                0, caller.TenantKey, 0, memberId, memberId, index == 0, now))
            .ToList();

    private static bool IsOnline(InterviewRecord interview) =>
        string.Equals(interview.Mode, "Online", StringComparison.OrdinalIgnoreCase);

    /// <summary>Also used by the employee Home interview card, so both surfaces label a row alike.</summary>
    internal static InterviewListItemDto MapListItem(InterviewSummaryRecord row) => new(
        row.Interview.Id.ToString(),
        new PersonReferenceDto(row.Interview.CandidateId.ToString(), row.CandidateName),
        row.JobTitle,
        row.Interview.RoundLabel,
        row.Interview.StartUtc,
        row.Interview.DurationMinutes,
        row.Interview.Mode,
        new StatusDto(
            row.Interview.Status,
            DescribeStatus(row.Interview),
            ToneFor(row.Interview),
            row.Interview.UpdatedUtc),
        [
            new ActionAvailabilityDto("reschedule", CanChange(row.Interview)),
            new ActionAvailabilityDto("cancel", CanChange(row.Interview)),
            new ActionAvailabilityDto("evaluate", !CanChange(row.Interview))
        ]);

    /// <summary>
    /// The reference card labels a past interview "Past Interview" rather than "Scheduled",
    /// so the label is derived from the clock, not only from the stored status.
    /// </summary>
    private static string DescribeStatus(InterviewRecord interview)
    {
        if (string.Equals(interview.Status, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            return "Cancelled";
        }

        if (string.Equals(interview.Status, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return "Completed";
        }

        return interview.EndUtc < DateTimeOffset.UtcNow ? "Past Interview" : "Upcoming";
    }

    private static string ToneFor(InterviewRecord interview) =>
        string.Equals(interview.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
            ? "danger"
            : "info";

    private static bool CanChange(InterviewRecord interview) =>
        string.Equals(interview.Status, "Scheduled", StringComparison.OrdinalIgnoreCase) &&
        interview.StartUtc > DateTimeOffset.UtcNow;

    private static string BuildRoundLabel(string roundCode) => roundCode.ToUpperInvariant() switch
    {
        "SCREENING" => "Round 1 - Screening",
        "TECHNICAL" => "Round 2 - Technical",
        "MANAGERIAL" => "Round 3 - Managerial",
        "HR" => "Round 4 - HR",
        _ => roundCode
    };

    private static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new MobileValidationException("TIMEZONE_UNKNOWN", $"'{timeZoneId}' is not a known time zone.");
        }
    }

    private static DateTimeOffset ToUtc(DateOnly day, TimeOnly time, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(time, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }

    private static long ParseRequiredId(string value) =>
        long.TryParse(value, out var parsed)
            ? parsed
            : throw new MobileNotFoundException("That identifier is not valid.");

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            return int.TryParse(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)), out var offset)
                ? Math.Max(0, offset)
                : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    private static string EncodeCursor(int offset) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(offset.ToString()));
}
