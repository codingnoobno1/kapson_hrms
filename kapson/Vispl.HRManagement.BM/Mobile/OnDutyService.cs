using System.Globalization;
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
/// Business travel that still counts as working time: a client meeting, a conference, a site
/// visit spanning one or more days.
///
/// Deliberately not a gate pass. A gate pass is permission to be off the premises, checked at a
/// physical gate; on duty is a statement to attendance and payroll that these days were worked
/// away from the desk. Nobody scans it, so it carries no verification token.
/// </summary>
public sealed class OnDutyService(
    IOnDutyRepository requests,
    IEmployeeRepository employees,
    ITF.Shared.INotificationPublisher notifications) : IOnDutyService
{
    /// <summary>Notification category for everything this service raises.</summary>
    private const string NotificationCategory = "OnDuty";

    /// <summary>Subject type, so a repeat about the same request is not stacked in an inbox.</summary>
    private const string NotificationSubject = "OnDuty";

    // The HR portal's own permission and the reporting-manager permission granted inside the
    // employee app name the same decision. Neither is the controller's job to choose between.
    private const string HrApprovePermission = "ONDUTY.APPROVE";
    private const string ManagerApprovePermission = "TEAM.ONDUTY.APPROVE";

    /// <summary>A request cannot cover an indefinite stretch.</summary>
    private const int MaximumDays = 30;

    public async Task<IReadOnlyList<OnDutyRowDto>> GetMineAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await requests.QueryAsync(
            caller.TenantKey,
            new OnDutyFilter(today.AddDays(-90), today.AddDays(90), [employee.Id], null),
            ct);

        return rows.Select(row => Map(row, employee, caller, employee.Id)).ToList();
    }

    public async Task<IReadOnlyList<OnDutyRowDto>> GetPendingAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        if (!caller.Has(HrApprovePermission) && !caller.Has(ManagerApprovePermission))
        {
            throw new MobileAccessDeniedException(HrApprovePermission);
        }

        var staff = await LoadStaffAsync(caller, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await requests.QueryAsync(
            caller.TenantKey,
            new OnDutyFilter(today.AddDays(-30), today.AddDays(90), staff.Keys.ToList(), "Pending"),
            ct);

        var ownEmployeeId = (await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct))?.Id;

        return rows
            .Where(row => staff.ContainsKey(row.EmployeeId))
            .Select(row => Map(row, staff[row.EmployeeId], caller, ownEmployeeId))
            .ToList();
    }

    public async Task<MutationResultDto> RequestAsync(
        MobileCallerContext caller, RequestOnDutyRequest request, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);

        if (request.ToDate < request.FromDate)
        {
            throw new MobileValidationException("RANGE_INVALID", "The request ends before it starts.");
        }

        var days = request.ToDate.DayNumber - request.FromDate.DayNumber + 1;
        if (days > MaximumDays)
        {
            throw new MobileValidationException(
                "RANGE_TOO_LONG", $"On duty covers at most {MaximumDays} days at a time.");
        }

        var overlapping = await requests.GetOverlappingAsync(
            caller.TenantKey, employee.Id, request.FromDate, request.ToDate, ct);

        if (overlapping.Count > 0)
        {
            var clash = overlapping.OrderBy(item => item.FromDate).First();
            throw new MobileValidationException(
                "OVERLAPPING_REQUEST",
                $"A {clash.State.ToLowerInvariant()} on duty request already covers {clash.FromDate:dd MMM yyyy}.");
        }

        var now = DateTimeOffset.UtcNow;

        var id = await requests.AddAsync(
            new OnDutyRequestRecord(
                0, caller.TenantKey, employee.Id, request.Purpose.Trim(), request.FromDate, request.ToDate,
                request.Location.Trim(), request.Reason.Trim(), "Pending",
                null, null, null, null, now, now),
            ct);

        // Addressed to everyone holding either approve permission, until an approver-resolution
        // service can narrow it to the one manager.
        await notifications.PublishToPermissionAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                NotificationCategory,
                "On duty requested",
                $"{DisplayName(employee)} requested on duty for {DescribeRange(request.FromDate, request.ToDate)}. " +
                $"{request.Purpose.Trim()}",
                $"/on-duty/{id}",
                "Normal",
                NotificationSubject,
                id.ToString(CultureInfo.InvariantCulture)),
            HrApprovePermission,
            ct);

        return new MutationResultDto(
            id.ToString(CultureInfo.InvariantCulture), "Pending", null, now,
            "Waiting for approval.");
    }

    public async Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller, string requestId, OnDutyDecisionRequest request, CancellationToken ct)
    {
        if (!caller.Has(HrApprovePermission) && !caller.Has(ManagerApprovePermission))
        {
            throw new MobileAccessDeniedException(HrApprovePermission);
        }

        var onDuty = await RequireAsync(caller, requestId, ct);

        if (onDuty.State != "Pending")
        {
            throw new MobileValidationException(
                "NOT_PENDING", $"This request is already {onDuty.State.ToLowerInvariant()}.");
        }

        var ownEmployeeId = (await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct))?.Id;
        if (ownEmployeeId == onDuty.EmployeeId)
        {
            throw new MobileValidationException(
                "SELF_APPROVAL", "An on duty request cannot be approved by the person who raised it.");
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
            throw new MobileValidationException("REASON_REQUIRED", "A rejection has to say why.");
        }

        var now = DateTimeOffset.UtcNow;

        await requests.UpdateAsync(
            onDuty with
            {
                State = state,
                DecidedByUserId = caller.UserId,
                DecidedByName = caller.EmployeeId ?? caller.UserId,
                DecidedUtc = now,
                DecisionComment = request.Comment,
                UpdatedUtc = now
            },
            ct);

        var applicant = await employees.DescribeAsync(caller.TenantKey, onDuty.EmployeeId, ct);

        if (applicant.CanBeNotified)
        {
            var body = state == "Approved"
                ? $"Your on duty request for {DescribeRange(onDuty.FromDate, onDuty.ToDate)} was approved."
                : $"Your on duty request for {DescribeRange(onDuty.FromDate, onDuty.ToDate)} was rejected. " +
                  $"{request.Comment?.Trim()}".TrimEnd();

            await notifications.PublishAsync(
                new ITF.Shared.NotificationRequest(
                    caller.TenantKey,
                    NotificationCategory,
                    state == "Approved" ? "On duty approved" : "On duty rejected",
                    body,
                    $"/on-duty/{onDuty.Id}",
                    state == "Approved" ? "Normal" : "High",
                    NotificationSubject,
                    $"{onDuty.Id}:{state.ToLowerInvariant()}"),
                [applicant.AccountId!],
                ct);
        }

        return new MutationResultDto(requestId, state, null, now, null);
    }

    public async Task<MutationResultDto> WithdrawAsync(
        MobileCallerContext caller, string requestId, CancellationToken ct)
    {
        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var onDuty = await RequireAsync(caller, requestId, ct);

        if (onDuty.EmployeeId != employee.Id)
        {
            throw new MobileValidationException(
                "NOT_YOURS", "Only the person who raised this request can withdraw it.");
        }

        if (onDuty.State is not ("Pending" or "Approved"))
        {
            throw new MobileValidationException(
                "NOT_WITHDRAWABLE", $"This request is already {onDuty.State.ToLowerInvariant()}.");
        }

        var now = DateTimeOffset.UtcNow;

        await requests.UpdateAsync(onDuty with { State = "Withdrawn", UpdatedUtc = now }, ct);

        await notifications.PublishToPermissionAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                NotificationCategory,
                "On duty withdrawn",
                $"{DisplayName(employee)} withdrew the on duty request for " +
                $"{DescribeRange(onDuty.FromDate, onDuty.ToDate)}.",
                $"/on-duty/{onDuty.Id}",
                "Low",
                NotificationSubject,
                $"{onDuty.Id}:withdrawn"),
            HrApprovePermission,
            ct);

        return new MutationResultDto(requestId, "Withdrawn", null, now, null);
    }

    // ---- Helpers ------------------------------------------------------------------------

    private static OnDutyRowDto Map(
        OnDutyRequestRecord onDuty, EmployeeRecord person, MobileCallerContext caller, long? ownEmployeeId)
    {
        var isOwner = ownEmployeeId == onDuty.EmployeeId;
        var isPending = onDuty.State == "Pending";
        var canApprove = caller.Has(HrApprovePermission) || caller.Has(ManagerApprovePermission);

        var actions = new List<ActionAvailabilityDto>
        {
            new("approve", !isOwner && isPending && canApprove,
                isOwner ? "A request cannot be approved by the person who raised it." : null),
            new("reject", !isOwner && isPending && canApprove,
                isOwner ? "A request cannot be rejected by the person who raised it." : null),
            new("withdraw", isOwner && onDuty.State is "Pending" or "Approved",
                isOwner ? null : "Only the requester can withdraw this request.")
        };

        return new OnDutyRowDto(
            onDuty.Id.ToString(CultureInfo.InvariantCulture),
            onDuty.EmployeeId.ToString(CultureInfo.InvariantCulture),
            DisplayName(person),
            person.DepartmentCode,
            person.JobTitle,
            onDuty.Purpose,
            onDuty.FromDate,
            onDuty.ToDate,
            onDuty.Location,
            onDuty.Reason,
            new StatusDto(onDuty.State, onDuty.State, ToneFor(onDuty.State), onDuty.UpdatedUtc),
            onDuty.DecidedByName,
            onDuty.DecidedUtc,
            onDuty.DecisionComment,
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

    private async Task<OnDutyRequestRecord> RequireAsync(
        MobileCallerContext caller, string requestId, CancellationToken ct)
    {
        if (!long.TryParse(requestId, out var id))
        {
            throw new MobileNotFoundException($"On duty request {requestId} was not found.");
        }

        return await requests.GetAsync(caller.TenantKey, id, ct)
               ?? throw new MobileNotFoundException($"On duty request {requestId} was not found.");
    }

    private async Task<EmployeeRecord> RequireOwnEmployeeAsync(
        MobileCallerContext caller, CancellationToken ct) =>
        await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct)
        ?? throw new MobileValidationException(
            "NO_EMPLOYEE_RECORD",
            "This account is not linked to an employee record, so it cannot hold an on duty request.");

    private async Task<Dictionary<long, EmployeeRecord>> LoadStaffAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var staff = await employees.QueryAsync(
            caller.TenantKey,
            new EmployeeQueryFilter(null, null, null, null, null, ActiveOnly: true),
            ct);

        return staff.ToDictionary(person => person.Id);
    }

    /// <summary>A range in words. One day reads as that day, not as a range to itself.</summary>
    private static string DescribeRange(DateOnly from, DateOnly to) =>
        from == to ? $"{from:d MMM yyyy}" : $"{from:d MMM} to {to:d MMM yyyy}";
}
