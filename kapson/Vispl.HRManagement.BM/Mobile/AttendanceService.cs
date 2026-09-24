using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.Shared;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Employee;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Punches are append-only. A day's status, worked time and exceptions are recomputed from the
/// surviving punches after every change, so the stored day can never disagree with the record
/// it is derived from.
/// </summary>
public sealed class AttendanceService(
    IAttendanceRepository attendance,
    IEmployeeRepository employees,
    IShiftScheduleService schedule,
    ITF.Shared.INotificationPublisher notifications,
    ITF.Admin.IEmployeeAppSettingsService appSettings,
    ITF.Shared.ITenantClock clock) : IAttendanceService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Notification category for everything this service raises.</summary>
    private const string NotificationCategory = "Attendance";

    /// <summary>Subject type, so a repeat for the same correction is not stacked in an inbox.</summary>
    private const string NotificationSubject = "AttendanceCorrection";

    private const string ViewPermission = "ATTENDANCE.VIEW";
    private const string TeamPermission = "ATTENDANCE.TEAM";
    private const string ApprovePermission = "ATTENDANCE.APPROVE";
    private const string ManagePermission = "ATTENDANCE.MANAGE";

    /// <summary>
    /// Used only if the schedule cannot resolve a shift at all — a tenant with no shifts and no
    /// default. Every real path reads the employee's own shift.
    /// </summary>
    private const int StandardDayMinutes = 8 * 60;
    private static readonly TimeOnly StandardStart = new(9, 0);
    private const int LateGraceMinutes = 15;

    /// <summary>How far under the expected day still counts as a full day.</summary>
    private const int ShortHoursToleranceMinutes = 15;

    // ---- Reads ------------------------------------------------------------

    public async Task<AttendanceDashboardDto> GetDashboardAsync(
        MobileCallerContext caller, DateOnly? date, CancellationToken ct)
    {
        Require(caller, TeamPermission);

        var day = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var counts = await attendance.CountByStatusAsync(caller.TenantKey, day, ct);

        // Fourteen days of attendance rate, so the sparkline shows a trend rather than a point.
        var trend = new List<decimal>();
        for (var offset = 13; offset >= 0; offset--)
        {
            var past = day.AddDays(-offset);
            var pastCounts = await attendance.CountByStatusAsync(caller.TenantKey, past, ct);
            var total = pastCounts.Values.Sum();
            var present = pastCounts.GetValueOrDefault("Present") + pastCounts.GetValueOrDefault("Partial");
            trend.Add(total == 0 ? 0 : Math.Round(present * 100m / total, 1));
        }

        return new AttendanceDashboardDto(day, counts, trend, DateTimeOffset.UtcNow);
    }

    public async Task<IReadOnlyList<AttendanceDayDto>> GetMineAsync(
        MobileCallerContext caller, AttendanceRangeQuery query, CancellationToken ct)
    {
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);
        var to = query.To ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var from = query.From ?? to.AddDays(-30);

        return await BuildDaysAsync(caller, employeeId, from, to, ct);
    }

    public async Task<CursorPage<AttendanceDayDto>> GetTeamAsync(
        MobileCallerContext caller, TeamAttendanceQuery query, CancellationToken ct)
    {
        Require(caller, TeamPermission);

        var day = query.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var reports = await ResolveReportsAsync(caller, ct);

        if (reports.Count == 0)
        {
            return Empty<AttendanceDayDto>(query.PageSize);
        }

        var days = await attendance.QueryDaysAsync(
            caller.TenantKey,
            new AttendanceDayFilter(day, day, null, reports, query.Status, false),
            ct);

        return Page(days.Select(MapDay).ToList(), query.Cursor, query.PageSize);
    }

    public async Task<CursorPage<AttendanceDayDto>> GetRegisterAsync(
        MobileCallerContext caller, DateOnly date, AttendanceRegisterQuery query, CancellationToken ct)
    {
        Require(caller, TeamPermission);

        // Department and location live on the employee, so the population is resolved first
        // and the register is then read for exactly those people.
        var filter = new EmployeeQueryFilter(query.Query, query.Department, query.Location, null, null);
        var staff = await employees.QueryAsync(caller.TenantKey, filter, ct);
        var ids = staff.Select(item => item.Id).ToList();

        if (ids.Count == 0)
        {
            return Empty<AttendanceDayDto>(query.PageSize);
        }

        var days = await attendance.QueryDaysAsync(
            caller.TenantKey,
            new AttendanceDayFilter(date, date, null, ids, query.Status, query.ExceptionsOnly),
            ct);

        return Page(days.Select(MapDay).ToList(), query.Cursor, query.PageSize);
    }

    public async Task<PunchStateDto> GetPunchStateAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);
        var last = await attendance.GetLastPunchAsync(caller.TenantKey, employeeId, ct);

        var isIn = last is not null && string.Equals(last.Type, "In", StringComparison.OrdinalIgnoreCase);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var shifts = await schedule.ResolveAsync(caller.TenantKey, [employeeId], today, today, ct);
        var expected = shifts.For(employeeId, today);

        var hint = expected is null
            ? $"A standard day is {StandardDayMinutes / 60} hours from {StandardStart:HH\\:mm}."
            : expected.IsWorkingDay
                ? $"{expected.ShiftName}: {expected.ExpectedMinutes / 60} hours from {expected.StartTime:HH\\:mm}."
                : $"{expected.ShiftName} does not run today.";

        return new PunchStateDto(
            isIn ? "In" : "Out",
            last is null ? null : MapPunch(last),
            isIn ? "out" : "in",
            DateTimeOffset.UtcNow,
            hint);
    }

    // ---- Punching ---------------------------------------------------------

    public async Task<PunchResultDto> PunchAsync(
        MobileCallerContext caller, PunchRequest request, CancellationToken ct)
    {
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);

        // HR portal → Settings → Employee App. Enforced here, not only hidden in the app.
        if (!(await appSettings.GetAsync(caller.TenantKey, ct)).AllowAppPunch)
        {
            throw new MobileValidationException(
                "APP_PUNCH_DISABLED", "Punching from the app has been turned off by HR. Use the attendance device.");
        }

        // Phase D, D-2: when HR requires location, a punch without a GPS fix is refused.
        if ((await appSettings.GetAsync(caller.TenantKey, ct)).RequireLocationForPunch &&
            (request.Latitude is null || request.Longitude is null))
        {
            throw new MobileValidationException(
                "LOCATION_REQUIRED", "HR requires your location when punching. Allow location access and try again.");
        }

        if (string.IsNullOrWhiteSpace(request.ClientEventId))
        {
            throw new MobileValidationException(
                "CLIENT_EVENT_REQUIRED", "A client event id is required so a retry cannot double-punch.");
        }

        // A retried request returns the original result rather than recording a second punch.
        var duplicate = await attendance.FindByClientEventAsync(
            caller.TenantKey, employeeId, request.ClientEventId, ct);

        if (duplicate is not null)
        {
            return new PunchResultDto(
                duplicate.Id.ToString(), duplicate.Type, duplicate.OccurredUtc,
                "This punch was already recorded.",
                string.Equals(duplicate.Type, "In", StringComparison.OrdinalIgnoreCase) ? "out" : "in");
        }

        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("in" or "out"))
        {
            throw new MobileValidationException("ACTION_INVALID", "A punch is either in or out.");
        }

        var last = await attendance.GetLastPunchAsync(caller.TenantKey, employeeId, ct);
        var lastWasIn = last is not null && string.Equals(last.Type, "In", StringComparison.OrdinalIgnoreCase);

        // Two ins or two outs in a row would make worked time meaningless.
        if (action == "in" && lastWasIn)
        {
            throw new MobileValidationException("ALREADY_IN", "You are already clocked in.");
        }

        if (action == "out" && !lastWasIn)
        {
            throw new MobileValidationException("NOT_CLOCKED_IN", "You are not clocked in.");
        }

        var now = DateTimeOffset.UtcNow;

        // The device clock is not trusted: a client time more than a few minutes from the
        // server's is ignored rather than silently accepted, because attendance drives pay.
        var occurred = request.ClientOccurredUtc == default
            ? now
            : request.ClientOccurredUtc;

        var drift = (now - occurred).Duration();
        var warning = drift > TimeSpan.FromMinutes(5)
            ? "The device clock differed from the server; the server time was recorded."
            : null;

        if (drift > TimeSpan.FromMinutes(5) || occurred > now)
        {
            occurred = now;
        }

        var workDate = DateOnly.FromDateTime(occurred.UtcDateTime);

        var punchId = await attendance.AddPunchAsync(
            new AttendancePunchRecord(
                0, caller.TenantKey, employeeId, workDate, action == "in" ? "In" : "Out",
                occurred, "Device", request.DeviceId, null,
                request.Latitude, request.Longitude, request.ClientEventId, null, now),
            ct);

        await RecomputeDayAsync(caller.TenantKey, employeeId, workDate, ct);

        return new PunchResultDto(
            punchId.ToString(), action == "in" ? "In" : "Out", occurred, warning,
            action == "in" ? "out" : "in");
    }

    // ---- Corrections ------------------------------------------------------

    public async Task<CursorPage<AttendanceCorrectionDto>> ListCorrectionsAsync(
        MobileCallerContext caller, AttendanceCorrectionListQuery query, CancellationToken ct)
    {
        // Without the approval permission a caller sees only their own requests.
        var scopeToSelf = !caller.Has(ApprovePermission);
        long? employeeId = scopeToSelf ? await RequireOwnEmployeeIdAsync(caller, ct) : null;

        var state = query.AwaitingMyDecisionOnly ? "Pending" : query.State;
        var rows = await attendance.QueryCorrectionsAsync(caller.TenantKey, employeeId, state, ct);

        // An approver's queue names each person, so a card says whose day it is.
        var names = new Dictionary<long, string>();
        if (!scopeToSelf)
        {
            foreach (var id in rows.Select(row => row.EmployeeId).Distinct())
            {
                names[id] = (await employees.DescribeAsync(caller.TenantKey, id, ct)).DisplayName;
            }
        }

        return Page(
            rows.Select(row => MapCorrection(row) with { EmployeeName = names.GetValueOrDefault(row.EmployeeId) }).ToList(),
            query.Cursor,
            query.PageSize);
    }

    public async Task<AttendanceCorrectionDto> GetCorrectionAsync(
        MobileCallerContext caller, string requestId, CancellationToken ct)
    {
        var correction = await LoadCorrectionAsync(caller, requestId, ct);
        return MapCorrection(correction);
    }

    public async Task<MutationResultDto> CreateCorrectionAsync(
        MobileCallerContext caller, CreateAttendanceCorrectionRequest request, CancellationToken ct)
    {
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        if (!(await appSettings.GetAsync(caller.TenantKey, ct)).AllowAttendanceCorrections)
        {
            throw new MobileValidationException(
                "CORRECTIONS_DISABLED", "Attendance corrections from the app have been turned off by HR.");
        }

        if (request.Date > today)
        {
            throw new MobileValidationException(
                "FUTURE_DATE", "A correction can only be raised for a day that has happened.");
        }

        // A window keeps the payroll period from being reopened indefinitely.
        if (request.Date < today.AddDays(-30))
        {
            throw new MobileValidationException(
                "TOO_OLD", "Corrections can be raised for the last 30 days only.");
        }

        if (request.RequestedPunches.Count == 0)
        {
            throw new MobileValidationException(
                "PUNCHES_REQUIRED", "State the punches you are asking for.");
        }

        var ordered = request.RequestedPunches.OrderBy(punch => punch.OccurredUtc).ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            var expected = index % 2 == 0 ? "in" : "out";
            if (!string.Equals(ordered[index].Type, expected, StringComparison.OrdinalIgnoreCase))
            {
                throw new MobileValidationException(
                    "PUNCHES_UNBALANCED", "Requested punches must alternate in, out, in, out.");
            }
        }

        var existing = await attendance.GetPendingCorrectionAsync(caller.TenantKey, employeeId, request.Date, ct);
        if (existing is not null)
        {
            throw new MobileValidationException(
                "ALREADY_PENDING", "A correction for that day is already awaiting a decision.");
        }

        var original = await attendance.GetPunchesAsync(
            caller.TenantKey, employeeId, request.Date, request.Date, ct);

        var now = DateTimeOffset.UtcNow;

        var id = await attendance.UpsertCorrectionAsync(
            new AttendanceCorrectionRecord(
                0, caller.TenantKey, employeeId, request.Date, request.ReasonCode, request.Comment,
                JsonSerializer.Serialize(original.Select(MapPunch), Json),
                JsonSerializer.Serialize(ordered, Json),
                "Pending", null, null, null, null, now, now),
            ct);

        // Raised after the correction is stored. Addressed to everyone holding the approve
        // permission until an approver-resolution service can narrow it to the one owner.
        var applicant = await employees.DescribeAsync(caller.TenantKey, employeeId, ct);

        await notifications.PublishToPermissionAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                NotificationCategory,
                "Attendance correction requested",
                $"{applicant.DisplayName} asked to correct {request.Date:d MMM yyyy}. " +
                $"{ordered.Count} {(ordered.Count == 1 ? "punch" : "punches")} proposed.",
                $"/attendance/corrections/{id}",
                "Normal",
                NotificationSubject,
                id.ToString()),
            ApprovePermission,
            ct);

        return new MutationResultDto(id.ToString(), "Pending", null, now);
    }

    public async Task<MutationResultDto> DecideCorrectionAsync(
        MobileCallerContext caller, string requestId, AttendanceCorrectionDecisionRequest request, CancellationToken ct)
    {
        Require(caller, ApprovePermission);

        var correction = await LoadCorrectionAsync(caller, requestId, ct);

        if (!string.Equals(correction.State, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new MobileValidationException(
                "NOT_PENDING", $"This correction is already {correction.State.ToLowerInvariant()}.");
        }

        // Nobody decides their own correction, however senior.
        var ownEmployeeId = await TryResolveOwnEmployeeIdAsync(caller, ct);
        if (ownEmployeeId == correction.EmployeeId)
        {
            throw new MobileValidationException(
                "SELF_APPROVAL", "A correction cannot be decided by the person who raised it.");
        }

        var approve = string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase);

        if (!approve && string.IsNullOrWhiteSpace(request.ReasonCode))
        {
            throw new MobileValidationException(
                "REASON_REQUIRED", "A reason is required to reject a correction.");
        }

        var now = DateTimeOffset.UtcNow;

        await attendance.UpsertCorrectionAsync(
            correction with
            {
                State = approve ? "Approved" : "Rejected",
                DecisionReasonCode = request.ReasonCode,
                DecisionComment = request.Comment,
                DecidedBy = caller.UserId,
                DecidedUtc = now,
                UpdatedUtc = now
            },
            ct);

        if (approve)
        {
            // The original punches are superseded rather than deleted, then the requested
            // ones are written as correction-sourced punches and the day recomputed.
            await attendance.SupersedePunchesAsync(
                caller.TenantKey, correction.EmployeeId, correction.WorkDate, correction.Id, ct);

            var requested = JsonSerializer.Deserialize<List<RequestedPunchDto>>(
                correction.RequestedPunchesJson, Json) ?? [];

            foreach (var punch in requested)
            {
                await attendance.AddPunchAsync(
                    new AttendancePunchRecord(
                        0, caller.TenantKey, correction.EmployeeId, correction.WorkDate,
                        punch.Type, punch.OccurredUtc, "Correction", null, null, null, null,
                        $"correction-{correction.Id}-{punch.Type}-{punch.OccurredUtc.Ticks}",
                        null, now),
                    ct);
            }

            await RecomputeDayAsync(caller.TenantKey, correction.EmployeeId, correction.WorkDate, ct);
        }

        // Told after the day has been recomputed, so the employee who opens the notification
        // sees the corrected day rather than the one they asked to change.
        var applicant = await employees.DescribeAsync(caller.TenantKey, correction.EmployeeId, ct);

        if (applicant.AccountId is { Length: > 0 } recipient)
        {
            var body = approve
                ? $"Your correction for {correction.WorkDate:d MMM yyyy} was approved and the day updated."
                : $"Your correction for {correction.WorkDate:d MMM yyyy} was rejected. " +
                  $"{request.Comment?.Trim()}".TrimEnd();

            await notifications.PublishAsync(
                new ITF.Shared.NotificationRequest(
                    caller.TenantKey,
                    NotificationCategory,
                    approve ? "Attendance correction approved" : "Attendance correction rejected",
                    body,
                    $"/attendance/corrections/{correction.Id}",
                    approve ? "Normal" : "High",
                    NotificationSubject,
                    $"{correction.Id}:{(approve ? "approved" : "rejected")}"),
                [recipient],
                ct);
        }

        return new MutationResultDto(requestId, approve ? "Approved" : "Rejected", null, now);
    }

    public async Task<MutationResultDto> ApplyHrCorrectionAsync(
        MobileCallerContext caller, HrAttendanceDayEditRequest request, CancellationToken ct)
    {
        Require(caller, ManagePermission);

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            throw new MobileValidationException(
                "REASON_REQUIRED", "Say why this day is being changed. The employee is shown the reason.");
        }

        var employeeId = ParseRequiredId(request.EmployeeId);
        var today = await clock.TodayAsync(caller.TenantKey, ct);

        if (request.Date > today)
        {
            throw new MobileValidationException("FUTURE_DATE", "A day that has not happened yet cannot be edited.");
        }

        // The same rule as a correction decision: nobody changes their own attendance.
        var ownEmployeeId = await TryResolveOwnEmployeeIdAsync(caller, ct);
        if (ownEmployeeId == employeeId)
        {
            throw new MobileValidationException("SELF_EDIT", "Your own attendance has to be changed by someone else.");
        }

        var zone = await clock.ResolveZoneAsync(caller.TenantKey, ct);
        var requested = new List<RequestedPunchDto>();

        if (!request.MarkAbsent)
        {
            if (request.CheckIn is null && request.CheckOut is null)
            {
                throw new MobileValidationException(
                    "PUNCHES_REQUIRED", "Give a check-in, a check-out, or mark the day absent.");
            }

            if (request.CheckIn is { } checkIn)
            {
                requested.Add(new RequestedPunchDto("In", ToUtc(request.Date, checkIn, zone)));
            }

            if (request.CheckOut is { } checkOut)
            {
                requested.Add(new RequestedPunchDto("Out", ToUtc(request.Date, checkOut, zone)));
            }

            if (requested.Count == 2 && requested[1].OccurredUtc <= requested[0].OccurredUtc)
            {
                throw new MobileValidationException("OUT_BEFORE_IN", "Check-out has to be after check-in.");
            }
        }

        var reason = request.Reason.Trim();
        var original = await attendance.GetPunchesAsync(caller.TenantKey, employeeId, request.Date, request.Date, ct);
        var now = DateTimeOffset.UtcNow;

        // An employee's own pending request for that day is overtaken by HR's change.
        var pending = await attendance.GetPendingCorrectionAsync(caller.TenantKey, employeeId, request.Date, ct);
        if (pending is not null)
        {
            await attendance.UpsertCorrectionAsync(
                pending with
                {
                    State = "Rejected",
                    DecisionReasonCode = "HR_EDIT",
                    DecisionComment = "Replaced by a change HR made to this day.",
                    DecidedBy = caller.UserId,
                    DecidedUtc = now,
                    UpdatedUtc = now
                },
                ct);
        }

        var correctionId = await attendance.UpsertCorrectionAsync(
            new AttendanceCorrectionRecord(
                0, caller.TenantKey, employeeId, request.Date, "HR_EDIT", reason,
                JsonSerializer.Serialize(original.Select(MapPunch), Json),
                JsonSerializer.Serialize(requested, Json),
                "Approved", "HR_EDIT", reason, caller.UserId, now, now, now),
            ct);

        await attendance.SupersedePunchesAsync(caller.TenantKey, employeeId, request.Date, correctionId, ct);

        foreach (var punch in requested)
        {
            await attendance.AddPunchAsync(
                new AttendancePunchRecord(
                    0, caller.TenantKey, employeeId, request.Date, punch.Type, punch.OccurredUtc,
                    "HrEdit", null, null, null, null, $"hr-edit-{correctionId}-{punch.Type}", null, now),
                ct);
        }

        await RecomputeDayAsync(caller.TenantKey, employeeId, request.Date, ct);

        var applicant = await employees.DescribeAsync(caller.TenantKey, employeeId, ct);

        if (applicant.AccountId is { Length: > 0 } recipient)
        {
            await notifications.PublishAsync(
                new ITF.Shared.NotificationRequest(
                    caller.TenantKey,
                    NotificationCategory,
                    "Attendance updated by HR",
                    $"HR updated your attendance for {request.Date:d MMM yyyy}. {reason}",
                    $"/attendance/corrections/{correctionId}",
                    "Normal",
                    NotificationSubject,
                    $"{correctionId}:hr-edit"),
                [recipient],
                ct);
        }

        return new MutationResultDto(correctionId.ToString(), "Approved", null, now);
    }

    /// <summary>A tenant wall-clock time on a date, as UTC.</summary>
    private static DateTimeOffset ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone) =>
        new(TimeZoneInfo.ConvertTimeToUtc(date.ToDateTime(time, DateTimeKind.Unspecified), zone), TimeSpan.Zero);

    public async Task<MutationResultDto> WithdrawCorrectionAsync(
        MobileCallerContext caller, string requestId, CancellationToken ct)
    {
        var correction = await LoadCorrectionAsync(caller, requestId, ct);
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);

        if (correction.EmployeeId != employeeId)
        {
            throw new MobileAccessDeniedException("own attendance correction");
        }

        if (!string.Equals(correction.State, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            throw new MobileValidationException(
                "NOT_PENDING", "Only a pending correction can be withdrawn.");
        }

        var now = DateTimeOffset.UtcNow;

        await attendance.UpsertCorrectionAsync(
            correction with { State = "Withdrawn", UpdatedUtc = now }, ct);

        // Approvers are told, so nobody works a queue item that no longer needs a decision.
        var applicant = await employees.DescribeAsync(caller.TenantKey, correction.EmployeeId, ct);

        await notifications.PublishToPermissionAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                NotificationCategory,
                "Attendance correction withdrawn",
                $"{applicant.DisplayName} withdrew the correction for " +
                $"{correction.WorkDate:d MMM yyyy}. No decision is needed.",
                $"/attendance/corrections/{correction.Id}",
                "Low",
                NotificationSubject,
                $"{correction.Id}:withdrawn"),
            ApprovePermission,
            ct);

        return new MutationResultDto(requestId, "Withdrawn", null, now);
    }

    // ---- Exceptions -------------------------------------------------------

    public async Task<CursorPage<AttendanceExceptionDto>> ListExceptionsAsync(
        MobileCallerContext caller, AttendanceExceptionListQuery query, CancellationToken ct)
    {
        var scopeToSelf = !caller.Has(TeamPermission);
        long? employeeId = scopeToSelf ? await RequireOwnEmployeeIdAsync(caller, ct) : null;

        var rows = await attendance.QueryExceptionsAsync(
            caller.TenantKey, employeeId, query.From, query.To, query.ExceptionType, query.State, ct);

        return Page(rows.Select(MapException).ToList(), query.Cursor, query.PageSize);
    }

    public async Task<AttendanceExceptionDto> GetExceptionAsync(
        MobileCallerContext caller, string exceptionId, CancellationToken ct) =>
        MapException(await LoadExceptionAsync(caller, exceptionId, ct));

    public async Task<MutationResultDto> ConfirmExceptionAsync(
        MobileCallerContext caller, string exceptionId, ConfirmAttendanceExceptionRequest request, CancellationToken ct)
    {
        var exception = await LoadExceptionAsync(caller, exceptionId, ct);
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);

        // Confirming is the employee's own statement about their day.
        if (exception.EmployeeId != employeeId)
        {
            throw new MobileAccessDeniedException("own attendance exception");
        }

        if (!string.Equals(exception.State, "Open", StringComparison.OrdinalIgnoreCase))
        {
            throw new MobileValidationException(
                "NOT_OPEN", $"This exception is already {exception.State.ToLowerInvariant()}.");
        }

        if (!request.Confirmed && string.IsNullOrWhiteSpace(request.Justification))
        {
            throw new MobileValidationException(
                "JUSTIFICATION_REQUIRED", "Explain why this exception is not correct.");
        }

        var now = DateTimeOffset.UtcNow;

        await attendance.UpsertExceptionAsync(
            exception with
            {
                State = "Confirmed",
                EmployeeJustification = request.Justification,
                UpdatedUtc = now
            },
            ct);

        return new MutationResultDto(exceptionId, "Confirmed", null, now);
    }

    public async Task<MutationResultDto> ResolveExceptionAsync(
        MobileCallerContext caller, string exceptionId, ResolveAttendanceExceptionRequest request, CancellationToken ct)
    {
        Require(caller, ApprovePermission);

        var exception = await LoadExceptionAsync(caller, exceptionId, ct);

        if (string.Equals(exception.State, "Approved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(exception.State, "Dismissed", StringComparison.OrdinalIgnoreCase))
        {
            throw new MobileValidationException(
                "ALREADY_RESOLVED", $"This exception is already {exception.State.ToLowerInvariant()}.");
        }

        var ownEmployeeId = await TryResolveOwnEmployeeIdAsync(caller, ct);
        if (ownEmployeeId == exception.EmployeeId)
        {
            throw new MobileValidationException(
                "SELF_APPROVAL", "An exception cannot be resolved by the person it belongs to.");
        }

        var approve = string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase);

        // Approving more overtime than was actually worked would inflate pay.
        var approvedMinutes = request.ApprovedDuration.HasValue
            ? (int)request.ApprovedDuration.Value.TotalMinutes
            : exception.DurationMinutes;

        if (approve && approvedMinutes > exception.DurationMinutes)
        {
            throw new MobileValidationException(
                "APPROVED_EXCEEDS_MEASURED",
                $"Approved time cannot exceed the {exception.DurationMinutes} minutes measured.");
        }

        var now = DateTimeOffset.UtcNow;

        await attendance.UpsertExceptionAsync(
            exception with
            {
                State = approve ? "Approved" : "Dismissed",
                ApprovedMinutes = approve ? approvedMinutes : 0,
                DecisionReasonCode = request.ReasonCode,
                DecisionComment = request.Comment,
                DecidedBy = caller.UserId,
                DecidedUtc = now,
                UpdatedUtc = now
            },
            ct);

        return new MutationResultDto(exceptionId, approve ? "Approved" : "Dismissed", null, now);
    }

    // ---- Day computation --------------------------------------------------

    /// <summary>
    /// Rebuilds a day from its surviving punches. Called after every change so the stored day
    /// and the punches behind it cannot drift apart.
    /// </summary>
    private async Task RecomputeDayAsync(
        string tenantKey, long employeeId, DateOnly workDate, CancellationToken ct)
    {
        var punches = await attendance.GetPunchesAsync(tenantKey, employeeId, workDate, workDate, ct);
        var now = DateTimeOffset.UtcNow;

        // What this person was expected to work that day. Everything below — lateness,
        // overtime, short hours, and whether an empty day is an absence at all — is judged
        // against this rather than against a fixed nine-to-five.
        var shifts = await schedule.ResolveAsync(tenantKey, [employeeId], workDate, workDate, ct);
        var expectedShift = shifts.For(employeeId, workDate);
        var expectedMinutes = expectedShift?.ExpectedMinutes ?? StandardDayMinutes;

        var worked = 0;
        DateTimeOffset? openedAt = null;
        var unbalanced = false;

        foreach (var punch in punches.OrderBy(item => item.OccurredUtc))
        {
            if (string.Equals(punch.Type, "In", StringComparison.OrdinalIgnoreCase))
            {
                // Two ins without an out between them means a punch is missing.
                if (openedAt is not null)
                {
                    unbalanced = true;
                }

                openedAt = punch.OccurredUtc;
            }
            else if (openedAt is { } opened)
            {
                worked += (int)(punch.OccurredUtc - opened).TotalMinutes;
                openedAt = null;
            }
            else
            {
                unbalanced = true;
            }
        }

        // Still clocked in at the end of a past day: the out punch never happened.
        var stillOpen = openedAt is not null;
        var isToday = workDate == DateOnly.FromDateTime(DateTime.UtcNow);

        var exceptions = new List<string>();

        if (punches.Count == 0)
        {
            exceptions.Add("NoPunches");
        }
        else
        {
            if (unbalanced || (stillOpen && !isToday))
            {
                exceptions.Add("MissingPunch");
            }

            var firstIn = punches.FirstOrDefault(item =>
                string.Equals(item.Type, "In", StringComparison.OrdinalIgnoreCase));

            // Lateness is judged against the shift this person actually worked, including its
            // own grace. Arriving at 09:45 is late on a 09:00 shift and early on a 10:00 one.
            if (firstIn is not null && expectedShift?.IsWorkingDay != false)
            {
                var start = expectedShift?.StartTime ?? StandardStart;
                var grace = expectedShift?.GraceMinutes ?? LateGraceMinutes;

                var expectedStart = workDate.ToDateTime(start, DateTimeKind.Utc);
                var lateBy = (int)(firstIn.OccurredUtc.UtcDateTime - expectedStart).TotalMinutes;

                if (lateBy > grace)
                {
                    exceptions.Add("LateIn");
                    await RaiseAsync(tenantKey, employeeId, workDate, "LateIn", lateBy, now, ct);
                }
            }

            if (!stillOpen)
            {
                if (worked > expectedMinutes)
                {
                    exceptions.Add("Overtime");
                    await RaiseAsync(tenantKey, employeeId, workDate, "Overtime", worked - expectedMinutes, now, ct);
                }
                else if (worked < expectedMinutes - ShortHoursToleranceMinutes && !isToday)
                {
                    exceptions.Add("ShortHours");
                    await RaiseAsync(tenantKey, employeeId, workDate, "ShortHours", expectedMinutes - worked, now, ct);
                }
            }
        }

        if (exceptions.Contains("MissingPunch"))
        {
            await RaiseAsync(tenantKey, employeeId, workDate, "MissingPunch", 0, now, ct);
        }

        // Anything no longer true is cleared, unless a person has already acted on it.
        await attendance.RemoveExceptionsAsync(tenantKey, employeeId, workDate, exceptions, ct);

        // A rest day with no punches is not an absence. Only a day the shift actually runs can
        // be missed, which is what makes a weekend stop reading as a company-wide absence.
        var status = punches.Count == 0
            ? (expectedShift is { IsWorkingDay: false } ? "WeeklyOff" : "Absent")
            : worked >= expectedMinutes - ShortHoursToleranceMinutes
                ? "Present"
                : "Partial";

        await attendance.UpsertDayAsync(
            new AttendanceDayRecord(
                0, tenantKey, employeeId, workDate, expectedShift?.ShiftCode, status, worked,
                expectedShift is { IsWorkingDay: false } ? 0 : expectedMinutes,
                JsonSerializer.Serialize(exceptions, Json), now),
            ct);
    }

    private Task RaiseAsync(
        string tenantKey, long employeeId, DateOnly workDate, string type, int minutes,
        DateTimeOffset now, CancellationToken ct) =>
        attendance.UpsertExceptionAsync(
            new AttendanceExceptionRecord(
                0, tenantKey, employeeId, workDate, type, minutes, "Open",
                "STANDARD_DAY", null, null, null, null, null, null, now, now),
            ct);

    // ---- Helpers ----------------------------------------------------------

    private async Task<IReadOnlyList<AttendanceDayDto>> BuildDaysAsync(
        MobileCallerContext caller, long employeeId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var days = await attendance.QueryDaysAsync(
            caller.TenantKey, new AttendanceDayFilter(from, to, employeeId, null, null, false), ct);

        var punches = await attendance.GetPunchesAsync(caller.TenantKey, employeeId, from, to, ct);
        var byDate = punches.GroupBy(item => item.WorkDate)
            .ToDictionary(group => group.Key, group => group.ToList());

        var zone = await clock.ResolveZoneAsync(caller.TenantKey, ct);
        var shifts = await schedule.ResolveAsync(caller.TenantKey, [employeeId], from, to, ct);

        return days
            .Select(day => MapDayWithBadge(day, byDate.GetValueOrDefault(day.WorkDate, []), shifts, zone))
            .ToList();
    }

    /// <summary>A day with its Recent Attendance chip and local check-in / check-out times.</summary>
    internal static AttendanceDayDto MapDayWithBadge(
        AttendanceDayRecord day, IReadOnlyList<AttendancePunchRecord> punches, ShiftSchedule shifts, TimeZoneInfo zone)
    {
        var live = punches
            .Where(punch => punch.SupersededByCorrectionId is null)
            .OrderBy(punch => punch.OccurredUtc)
            .ToList();

        var firstIn = live.FirstOrDefault(punch => string.Equals(punch.Type, "In", StringComparison.OrdinalIgnoreCase));
        var lastOut = live.LastOrDefault(punch => string.Equals(punch.Type, "Out", StringComparison.OrdinalIgnoreCase));
        var delay = firstIn is null ? null : shifts.DelayMinutes(day.EmployeeId, day.WorkDate, firstIn.OccurredUtc);

        return MapDay(day, punches) with
        {
            Badge = BadgeFor(day.Status, delay),
            CheckIn = Local(firstIn, zone),
            CheckOut = Local(lastOut, zone)
        };
    }

    internal static StatusDto BadgeFor(string status, int? delayMinutes)
    {
        var value = status.ToLowerInvariant();

        if (value.Contains("partial") || value.Contains("half"))
        {
            return new StatusDto("half-day", "Half Day", "warning");
        }

        if (value.Contains("leave"))
        {
            return new StatusDto("leave", "Leave", "pending");
        }

        if (value.Contains("holiday"))
        {
            return new StatusDto("holiday", "Holiday", "info");
        }

        if (value.Contains("off") || value.Contains("weekend"))
        {
            return new StatusDto("weekly-off", "Weekly Off", "muted");
        }

        if (value.Contains("absent"))
        {
            return new StatusDto("absent", "Absent", "danger");
        }

        if (value.Contains("present"))
        {
            return delayMinutes > 0
                ? new StatusDto("late", "Late", "danger")
                : new StatusDto("on-time", "On Time", "success");
        }

        return new StatusDto(value, status, "muted");
    }

    private static TimeOnly? Local(AttendancePunchRecord? punch, TimeZoneInfo zone) =>
        punch is null ? null : TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(punch.OccurredUtc, zone).DateTime);

    /// <summary>
    /// Everyone reporting to the caller. Without an employee record of their own the caller
    /// has no reports, which is different from having none by permission.
    /// </summary>
    private async Task<List<long>> ResolveReportsAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var managerId = await TryResolveOwnEmployeeIdAsync(caller, ct);
        if (managerId is null)
        {
            return [];
        }

        var reports = await employees.QueryAsync(
            caller.TenantKey, new EmployeeQueryFilter(null, null, null, null, managerId), ct);

        return reports.Select(item => item.Id).ToList();
    }

    private async Task<AttendanceCorrectionRecord> LoadCorrectionAsync(
        MobileCallerContext caller, string requestId, CancellationToken ct)
    {
        var correction = await attendance.GetCorrectionAsync(caller.TenantKey, ParseRequiredId(requestId), ct)
            ?? throw new MobileNotFoundException("That correction no longer exists.");

        if (!caller.Has(ApprovePermission))
        {
            var ownEmployeeId = await TryResolveOwnEmployeeIdAsync(caller, ct);
            if (ownEmployeeId != correction.EmployeeId)
            {
                throw new MobileAccessDeniedException(ApprovePermission);
            }
        }

        return correction;
    }

    private async Task<AttendanceExceptionRecord> LoadExceptionAsync(
        MobileCallerContext caller, string exceptionId, CancellationToken ct)
    {
        var exception = await attendance.GetExceptionAsync(caller.TenantKey, ParseRequiredId(exceptionId), ct)
            ?? throw new MobileNotFoundException("That exception no longer exists.");

        if (!caller.Has(TeamPermission))
        {
            var ownEmployeeId = await TryResolveOwnEmployeeIdAsync(caller, ct);
            if (ownEmployeeId != exception.EmployeeId)
            {
                throw new MobileAccessDeniedException(TeamPermission);
            }
        }

        return exception;
    }

    /// <summary>
    /// Self-service needs an employee record, resolved from the caller's identity account.
    /// The token's employee claim carries a display number, not the key, so ownership is
    /// established against the employee table rather than against a claim.
    ///
    /// An HR account with no employee row cannot punch, and saying so beats recording
    /// attendance against nobody.
    /// </summary>
    private async Task<long> RequireOwnEmployeeIdAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var employee = await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct);

        return employee?.Id ?? throw new MobileValidationException(
            "NO_EMPLOYEE_RECORD",
            "This account is not linked to an employee record, so it has no attendance of its own.");
    }

    private async Task<long?> TryResolveOwnEmployeeIdAsync(MobileCallerContext caller, CancellationToken ct) =>
        (await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct))?.Id;

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }

    private static AttendanceDayDto MapDay(AttendanceDayRecord day) => MapDay(day, []);

    private static AttendanceDayDto MapDay(
        AttendanceDayRecord day, IReadOnlyList<AttendancePunchRecord> punches) => new(
        day.WorkDate,
        day.ShiftCode,
        new StatusDto(day.Status, day.Status, ToneFor(day.Status), day.UpdatedUtc),
        punches.Select(MapPunch).ToList(),
        TimeSpan.FromMinutes(day.WorkedMinutes),
        TimeSpan.FromMinutes(day.ExpectedMinutes),
        JsonSerializer.Deserialize<List<string>>(day.ExceptionsJson, Json) ?? [],
        [new ActionAvailabilityDto("request-correction", true)]);

    private static AttendancePunchDto MapPunch(AttendancePunchRecord punch) => new(
        punch.Id.ToString(), punch.Type, punch.OccurredUtc, punch.Source, punch.LocationLabel);

    private static AttendanceCorrectionDto MapCorrection(AttendanceCorrectionRecord correction) => new(
        correction.Id.ToString(),
        correction.WorkDate,
        JsonSerializer.Deserialize<List<AttendancePunchDto>>(correction.OriginalPunchesJson, Json) ?? [],
        (JsonSerializer.Deserialize<List<RequestedPunchDto>>(correction.RequestedPunchesJson, Json) ?? [])
            .Select(punch => new AttendancePunchDto("0", punch.Type, punch.OccurredUtc, "Requested", null))
            .ToList(),
        correction.Comment ?? correction.ReasonCode,
        new StatusDto(correction.State, correction.State, ToneFor(correction.State), correction.DecidedUtc),
        [new WorkflowStepDto("review", "Attendance approval", correction.State, null, null, correction.DecidedUtc)],
        null);

    private static AttendanceExceptionDto MapException(AttendanceExceptionRecord exception) => new(
        exception.Id.ToString(),
        exception.EmployeeId.ToString(),
        exception.WorkDate,
        exception.ExceptionType,
        TimeSpan.FromMinutes(exception.DurationMinutes),
        new StatusDto(exception.State, exception.State, ToneFor(exception.State), exception.DecidedUtc),
        [],
        exception.PolicyCode,
        [
            new ActionAvailabilityDto("confirm", string.Equals(exception.State, "Open", StringComparison.OrdinalIgnoreCase)),
            new ActionAvailabilityDto("resolve", !string.Equals(exception.State, "Approved", StringComparison.OrdinalIgnoreCase) &&
                                                 !string.Equals(exception.State, "Dismissed", StringComparison.OrdinalIgnoreCase))
        ],
        null);

    private static string ToneFor(string state) => state switch
    {
        "Present" or "Approved" => "success",
        "Absent" or "Rejected" or "Dismissed" => "danger",
        "Partial" or "Pending" or "Open" => "pending",

        // A booked day off and a day the shift never ran are both correct outcomes, not
        // amber ones. Only an unexplained empty day is a problem.
        "Leave" or "WeeklyOff" => "info",
        _ => "neutral"
    };

    private static CursorPage<T> Empty<T>(int pageSize) =>
        new([], null, false, Math.Clamp(pageSize, 1, 100), 0);

    private static CursorPage<T> Page<T>(IReadOnlyList<T> all, string? cursor, int requestedSize)
    {
        var pageSize = Math.Clamp(requestedSize, 1, 100);
        var offset = DecodeCursor(cursor);
        var page = all.Skip(offset).Take(pageSize).ToList();
        var next = offset + page.Count;
        var hasMore = next < all.Count;

        return new CursorPage<T>(page, hasMore ? EncodeCursor(next) : null, hasMore, pageSize, all.Count);
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
