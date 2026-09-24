using System.Globalization;
using System.Security.Cryptography;
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
/// Permission to be off site, granted in advance and checked at the gate.
///
/// The verification token is the important part. It is issued only on approval, is random, and
/// is not the employee number: the number on an identity card can be photographed by anyone, so
/// it identifies a person but proves nothing about what they are allowed to do.
/// </summary>
public sealed class GatePassService(
    IMovementRepository movements,
    IEmployeeRepository employees,
    Vispl.HRManagement.BM.ITF.Shared.ITenantClock clock,
    ITF.Shared.INotificationPublisher notifications,
    ITF.Admin.IEmployeeAppSettingsService appSettings) : IGatePassService
{
    /// <summary>Notification category for everything this service raises.</summary>
    private const string NotificationCategory = "GatePass";

    /// <summary>Subject type, so a repeat for the same pass is not stacked in an inbox.</summary>
    private const string NotificationSubject = "GatePass";

    private const string TeamPermission = "GATEPASS.TEAM";
    private const string ApprovePermission = "GATEPASS.APPROVE";
    private const string VerifyPermission = "GATEPASS.VERIFY";

    /// <summary>A pass cannot be requested for an indefinite stretch.</summary>
    private const int MaximumPassDays = 30;

    public async Task<GatePassBoardDto> GetBoardAsync(
        MobileCallerContext caller, GatePassQuery query, CancellationToken ct)
    {
        Require(caller, TeamPermission);

        var staff = await LoadStaffAsync(caller, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var passes = await movements.QueryPassesAsync(
            caller.TenantKey,
            new GatePassFilter(
                query.From ?? today.AddDays(-30),
                query.To ?? today.AddDays(60),
                staff.Keys.ToList(),
                query.State,
                query.PassType),
            ct);

        var ownEmployeeId = (await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct))?.Id;

        var rows = new List<GatePassRowDto>();

        foreach (var pass in passes.Where(pass => staff.ContainsKey(pass.EmployeeId)))
        {
            var scans = await movements.GetScansAsync(caller.TenantKey, pass.Id, ct);
            rows.Add(Map(pass, staff[pass.EmployeeId], scans.Count, caller, ownEmployeeId));
        }

        return new GatePassBoardDto(
            Page(rows, query.Cursor, query.PageSize),
            rows.Count(row => row.Status.Code == "Pending"),
            DateTimeOffset.UtcNow);
    }

    public async Task<GatePassRowDto> GetAsync(
        MobileCallerContext caller, string passId, CancellationToken ct)
    {
        var pass = await RequirePassAsync(caller, passId, ct);
        var ownEmployeeId = (await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct))?.Id;

        // Own pass, or the right to see the team's. Nothing else.
        if (ownEmployeeId != pass.EmployeeId && !caller.Has(TeamPermission) && !caller.Has(ApprovePermission))
        {
            throw new MobileAccessDeniedException(TeamPermission);
        }

        var person = await employees.GetAsync(caller.TenantKey, pass.EmployeeId, ct)
                     ?? throw new MobileNotFoundException($"Employee {pass.EmployeeId} was not found.");

        var scans = await movements.GetScansAsync(caller.TenantKey, pass.Id, ct);
        return Map(pass, person, scans.Count, caller, ownEmployeeId);
    }

    public async Task<IReadOnlyList<GatePassRowDto>> GetMineAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var passes = await movements.QueryPassesAsync(
            caller.TenantKey,
            new GatePassFilter(today.AddDays(-90), today.AddDays(90), [employee.Id], null, null),
            ct);

        var rows = new List<GatePassRowDto>();

        foreach (var pass in passes)
        {
            var scans = await movements.GetScansAsync(caller.TenantKey, pass.Id, ct);
            rows.Add(Map(pass, employee, scans.Count, caller, employee.Id));
        }

        return rows;
    }

    public async Task<MutationResultDto> RequestAsync(
        MobileCallerContext caller, RequestGatePassRequest request, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);

        if (!(await appSettings.GetAsync(caller.TenantKey, ct)).AllowGatePassRequests)
        {
            throw new MobileValidationException(
                "GATEPASS_REQUESTS_DISABLED", "Gate pass requests from the app have been turned off by HR.");
        }

        if (request.ToDate < request.FromDate)
        {
            throw new MobileValidationException("RANGE_INVALID", "The pass ends before it starts.");
        }

        var days = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;

        // The type has to match the range, or a "day pass" spanning a fortnight would sail
        // through an approver reading only the label.
        if (request.PassType == "DayPass" && days > 1)
        {
            throw new MobileValidationException(
                "TYPE_MISMATCH", "A day pass covers one day. Request a multi-day pass for a range.");
        }

        if (request.PassType == "MultiDayPass" && days == 1)
        {
            throw new MobileValidationException(
                "TYPE_MISMATCH", "A multi-day pass covers more than one day. Request a day pass instead.");
        }

        if (days > MaximumPassDays)
        {
            throw new MobileValidationException(
                "RANGE_TOO_LONG", $"A gate pass covers at most {MaximumPassDays} days.");
        }

        if (request.FromTime is { } fromTime && request.ToTime is { } toTime && toTime <= fromTime)
        {
            throw new MobileValidationException(
                "TIME_RANGE_INVALID", "The pass end time is not after its start time.");
        }

        var overlapping = await movements.GetOverlappingPassesAsync(
            caller.TenantKey, employee.Id, request.FromDate, request.ToDate, ct);

        if (overlapping.Count > 0)
        {
            var clash = overlapping.OrderBy(item => item.FromDate).First();
            throw new MobileValidationException(
                "OVERLAPPING_PASS",
                $"A {clash.State.ToLowerInvariant()} pass already covers {clash.FromDate:dd MMM yyyy}.");
        }

        var now = DateTimeOffset.UtcNow;

        var id = await movements.AddPassAsync(
            new GatePassRecord(
                0, caller.TenantKey, employee.Id, request.PassType, request.FromDate, request.ToDate,
                request.FromTime, request.ToTime, request.Purpose.Trim(),
                request.DestinationLabel?.Trim(), "Pending", null, null, null, null, null, now, now),
            ct);

        // Raised after the pass is stored. Addressed to everyone holding the approve permission
        // until an approver-resolution service can narrow it to the one owner.
        await notifications.PublishToPermissionAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                NotificationCategory,
                "Gate pass requested",
                $"{DisplayName(employee)} requested a gate pass for {DescribeRange(request.FromDate, request.ToDate)}. " +
                $"{request.Purpose.Trim()}",
                $"/gate-passes/{id}",
                "Normal",
                NotificationSubject,
                id.ToString(CultureInfo.InvariantCulture)),
            ApprovePermission,
            ct);

        return new MutationResultDto(
            id.ToString(CultureInfo.InvariantCulture), "Pending", null, now,
            "Waiting for approval. The pass cannot be used until it is approved.");
    }

    public async Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller, string passId, GatePassDecisionRequest request, CancellationToken ct)
    {
        Require(caller, ApprovePermission);

        var pass = await RequirePassAsync(caller, passId, ct);

        if (pass.State != "Pending")
        {
            throw new MobileValidationException(
                "NOT_PENDING", $"This pass is already {pass.State.ToLowerInvariant()}.");
        }

        var ownEmployeeId = (await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct))?.Id;
        if (ownEmployeeId == pass.EmployeeId)
        {
            throw new MobileValidationException(
                "SELF_APPROVAL", "A gate pass cannot be approved by the person who requested it.");
        }

        var decision = request.Decision?.Trim().ToLowerInvariant();
        var state = decision switch
        {
            "approve" => "Approved",
            "reject" => "Rejected",
            _ => throw new MobileValidationException(
                "DECISION_INVALID", "A decision is either approve or reject.")
        };

        if (state == "Rejected" && string.IsNullOrWhiteSpace(request.Comment))
        {
            throw new MobileValidationException(
                "REASON_REQUIRED", "A rejection has to say why.");
        }

        var now = DateTimeOffset.UtcNow;

        // The token exists only on an approved pass, so a rejected one carries nothing a gate
        // could accept even if the row were read directly.
        var token = state == "Approved"
            ? Convert.ToHexString(RandomNumberGenerator.GetBytes(16))
            : null;

        await movements.UpdatePassAsync(
            pass with
            {
                State = state,
                VerificationToken = token,
                DecidedByUserId = caller.UserId,
                DecidedByName = caller.EmployeeId ?? caller.UserId,
                DecidedUtc = now,
                DecisionComment = request.Comment,
                UpdatedUtc = now
            },
            ct);

        // The person who asked is told the outcome. An approval says the pass is usable now,
        // because a pass nobody knows was granted is a pass nobody carries to the gate.
        var applicant = await employees.DescribeAsync(caller.TenantKey, pass.EmployeeId, ct);

        if (applicant.CanBeNotified)
        {
            var body = state == "Approved"
                ? $"Your gate pass for {DescribeRange(pass.FromDate, pass.ToDate)} was approved " +
                  "and is ready to show at the gate."
                : $"Your gate pass for {DescribeRange(pass.FromDate, pass.ToDate)} was rejected. " +
                  $"{request.Comment?.Trim()}".TrimEnd();

            await notifications.PublishAsync(
                new ITF.Shared.NotificationRequest(
                    caller.TenantKey,
                    NotificationCategory,
                    state == "Approved" ? "Gate pass approved" : "Gate pass rejected",
                    body,
                    $"/gate-passes/{pass.Id}",
                    state == "Approved" ? "Normal" : "High",
                    NotificationSubject,
                    $"{pass.Id}:{state.ToLowerInvariant()}"),
                [applicant.AccountId!],
                ct);
        }

        return new MutationResultDto(passId, state, null, now, null);
    }

    public async Task<MutationResultDto> WithdrawAsync(
        MobileCallerContext caller, string passId, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var pass = await RequirePassAsync(caller, passId, ct);

        if (pass.EmployeeId != employee.Id)
        {
            throw new MobileValidationException(
                "NOT_YOURS", "Only the person who requested this pass can withdraw it.");
        }

        if (pass.State is not ("Pending" or "Approved"))
        {
            throw new MobileValidationException(
                "NOT_WITHDRAWABLE", $"This pass is already {pass.State.ToLowerInvariant()}.");
        }

        var now = DateTimeOffset.UtcNow;

        // Withdrawing clears the token, so a pass someone changed their mind about stops
        // opening gates immediately rather than at the end of its date range.
        await movements.UpdatePassAsync(
            pass with { State = "Withdrawn", VerificationToken = null, UpdatedUtc = now }, ct);

        // Approvers are told whether or not it had been decided: a pending one leaves their
        // queue, and an approved one is a pass the gate should no longer expect to see.
        await notifications.PublishToPermissionAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                NotificationCategory,
                "Gate pass withdrawn",
                $"{DisplayName(employee)} withdrew the gate pass for " +
                $"{DescribeRange(pass.FromDate, pass.ToDate)}.",
                $"/gate-passes/{pass.Id}",
                "Low",
                NotificationSubject,
                $"{pass.Id}:withdrawn"),
            ApprovePermission,
            ct);

        return new MutationResultDto(passId, "Withdrawn", null, now, null);
    }

    public async Task<GatePassCredentialDto> GetCredentialAsync(
        MobileCallerContext caller, string passId, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var pass = await RequirePassAsync(caller, passId, ct);

        // The token is shown to its holder and to nobody else — not to a manager, not to HR.
        // Anyone who can read it can present it at a gate.
        if (pass.EmployeeId != employee.Id)
        {
            throw new MobileValidationException(
                "NOT_YOURS", "A pass credential is only shown to the person it was issued to.");
        }

        if (pass.State != "Approved" || string.IsNullOrWhiteSpace(pass.VerificationToken))
        {
            throw new MobileValidationException(
                "NOT_APPROVED", "This pass has not been approved, so it has no credential.");
        }

        return new GatePassCredentialDto(
            passId, pass.VerificationToken, pass.FromDate, pass.ToDate);
    }

    public async Task<GatePassScanResultDto> ScanAsync(
        MobileCallerContext caller, ScanGatePassRequest request, CancellationToken ct)
    {
        Require(caller, VerifyPermission);

        // The gate's own date, not the server's. In IST these differ for the first five
        // and a half hours of every day, which would refuse a valid pass every morning.
        var today = await clock.TodayAsync(caller.TenantKey, ct);
        var pass = await movements.FindPassByTokenAsync(caller.TenantKey, request.Token.Trim(), ct);

        // An unknown token says only that it is not valid. Telling a scanner why would let
        // someone probe for tokens that exist but are out of date.
        if (pass is null)
        {
            return new GatePassScanResultDto(
                false, "Unknown", null, null, null, null, "This pass is not recognised.");
        }

        if (!pass.IsUsableOn(today))
        {
            var reason = pass.State != "Approved"
                ? $"This pass is {pass.State.ToLowerInvariant()}."
                : $"This pass is valid {pass.FromDate:dd MMM} to {pass.ToDate:dd MMM}, not today.";

            return new GatePassScanResultDto(
                false, pass.State == "Approved" ? "OutOfDate" : pass.State,
                null, null, pass.PassType, pass.ToDate, reason);
        }

        var person = await employees.GetAsync(caller.TenantKey, pass.EmployeeId, ct);

        await movements.AddScanAsync(
            new GatePassScanRecord(
                0, caller.TenantKey, pass.Id, request.Direction, DateTimeOffset.UtcNow,
                caller.UserId, request.GateLabel?.Trim()),
            ct);

        return new GatePassScanResultDto(
            true,
            "Valid",
            person is null ? null : DisplayName(person),
            person?.EmployeeNumber,
            pass.PassType,
            pass.ToDate,
            $"Valid until {pass.ToDate:dd MMM yyyy}.");
    }

    // ---- Helpers ----------------------------------------------------------

    private static GatePassRowDto Map(
        GatePassRecord pass, EmployeeRecord person, int scanCount,
        MobileCallerContext caller, long? ownEmployeeId)
    {
        var isOwner = ownEmployeeId == pass.EmployeeId;
        var isPending = pass.State == "Pending";

        var actions = new List<ActionAvailabilityDto>
        {
            new("approve", !isOwner && isPending && caller.Has(ApprovePermission),
                isOwner ? "A pass cannot be approved by the person who requested it." : null),
            new("reject", !isOwner && isPending && caller.Has(ApprovePermission),
                isOwner ? "A pass cannot be rejected by the person who requested it." : null),
            new("withdraw", isOwner && pass.State is "Pending" or "Approved",
                isOwner ? null : "Only the requester can withdraw this pass.")
        };

        return new GatePassRowDto(
            pass.Id.ToString(CultureInfo.InvariantCulture),
            pass.EmployeeId.ToString(CultureInfo.InvariantCulture),
            DisplayName(person),
            person.DepartmentCode,
            person.JobTitle,
            pass.PassType,
            pass.FromDate,
            pass.ToDate,
            pass.FromTime,
            pass.ToTime,
            pass.Purpose,
            pass.DestinationLabel,
            new StatusDto(pass.State, pass.State, ToneFor(pass.State), pass.UpdatedUtc),
            pass.DecidedByName,
            pass.DecidedUtc,
            pass.DecisionComment,
            scanCount,
            actions);
    }

    private static string ToneFor(string state) => state switch
    {
        "Approved" => "success",
        "Rejected" => "danger",
        "Pending" => "warning",
        _ => "muted"
    };

    private static string DisplayName(EmployeeRecord person) =>
        string.IsNullOrWhiteSpace(person.PreferredName)
            ? $"{person.LegalFirstName} {person.LegalLastName}".Trim()
            : person.PreferredName;

    private async Task<GatePassRecord> RequirePassAsync(
        MobileCallerContext caller, string passId, CancellationToken ct)
    {
        if (!long.TryParse(passId, out var id))
        {
            throw new MobileNotFoundException($"Gate pass {passId} was not found.");
        }

        return await movements.GetPassAsync(caller.TenantKey, id, ct)
               ?? throw new MobileNotFoundException($"Gate pass {passId} was not found.");
    }

    private async Task<EmployeeRecord> RequireOwnEmployeeAsync(
        MobileCallerContext caller, CancellationToken ct) =>
        await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct)
        ?? throw new MobileValidationException(
            "NO_EMPLOYEE_RECORD",
            "This account is not linked to an employee record, so it cannot hold a gate pass.");

    private async Task<Dictionary<long, EmployeeRecord>> LoadStaffAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var staff = await employees.QueryAsync(
            caller.TenantKey,
            new EmployeeQueryFilter(null, null, null, null, null, ActiveOnly: true),
            ct);

        return staff.ToDictionary(person => person.Id);
    }

    /// <summary>
    /// A pass range in words. One day reads as that day rather than as a range to itself,
    /// because "12 Sep to 12 Sep" reads as a mistake.
    /// </summary>
    private static string DescribeRange(DateOnly from, DateOnly to) =>
        from == to ? $"{from:d MMM yyyy}" : $"{from:d MMM} to {to:d MMM yyyy}";

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
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
            page, hasMore ? next.ToString(CultureInfo.InvariantCulture) : null, hasMore, size, items.Count);
    }
}
