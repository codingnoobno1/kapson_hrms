using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
/// Leave, from balance to decision.
///
/// Two rules shape everything here. Chargeable days are always computed on the server, never
/// accepted from the client, because the number of days is what the balance is spent in. And
/// the days a request charges are stored as dates, so a second request cannot book a day that
/// is already claimed.
/// </summary>
public sealed class LeaveService(
    ILeaveRepository leave,
    IEmployeeRepository employees,
    ITF.Shared.INotificationPublisher notifications) : IMobileLeaveService, ILeavePolicyService
{
    /// <summary>Notification category for everything this service raises.</summary>
    private const string NotificationCategory = "Leave";

    /// <summary>Subject type, so a repeat for the same request is not stacked in an inbox.</summary>
    private const string NotificationSubject = "LeaveRequest";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private const string ViewPermission = "LEAVE.VIEW";
    private const string TeamPermission = "LEAVE.TEAM";
    private const string ApprovePermission = "LEAVE.APPROVE";

    /// <summary>How long a server calculation stays valid before it must be recomputed.</summary>
    private static readonly TimeSpan CalculationLifetime = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Signs calculation tokens. Generated per process: the token is short-lived and a rejected
    /// token simply asks the client to recalculate, so there is no secret to manage. A
    /// multi-instance deployment would need a shared key here.
    /// </summary>
    private static readonly byte[] CalculationKey = RandomNumberGenerator.GetBytes(32);

    // ---- Balances ---------------------------------------------------------

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetDashboardAsync(
        MobileCallerContext caller, CancellationToken ct) =>
        await GetBalancesAsync(caller, new LeaveBalanceQuery(), ct);

    public async Task<IReadOnlyList<LeaveBalanceDto>> GetBalancesAsync(
        MobileCallerContext caller, LeaveBalanceQuery query, CancellationToken ct)
    {
        var employeeId = await ResolveSubjectAsync(caller, query.EmployeeId, ct);
        var year = query.Year ?? DateTime.UtcNow.Year;

        var policies = await leave.GetPoliciesAsync(caller.TenantKey, true, ct);
        var granted = await leave.GetBalancesAsync(caller.TenantKey, employeeId, year, ct);
        var consumption = await leave.GetConsumptionAsync(caller.TenantKey, employeeId, year, ct);

        var results = new List<LeaveBalanceDto>();

        foreach (var policy in policies)
        {
            var balance = granted.FirstOrDefault(item => item.PolicyId == policy.Id);
            var used = consumption.FirstOrDefault(item => item.PolicyId == policy.Id);

            // An employee with no balance row has not been granted this type yet; it is shown
            // at zero rather than hidden, so the absence is visible rather than silent.
            var entitled = balance?.Entitled ?? 0m;
            var accrued = balance?.Accrued ?? 0m;
            var carried = balance?.CarriedForward ?? 0m;
            var adjustment = balance?.AdjustmentDays ?? 0m;

            var usedDays = used?.UsedDays ?? 0m;
            var pendingDays = used?.PendingDays ?? 0m;

            results.Add(new LeaveBalanceDto(
                policy.Id.ToString(CultureInfo.InvariantCulture),
                policy.Code,
                policy.Name,
                year,
                entitled,
                accrued + carried + adjustment,
                usedDays,
                pendingDays,
                accrued + carried + adjustment - usedDays - pendingDays,
                policy.Unit,
                LeaveTones.Normalize(policy.ToneKey),
                policy.Emoji));
        }

        return results;
    }

    // ---- Requests ---------------------------------------------------------

    public async Task<CursorPage<LeaveRequestListItemDto>> ListRequestsAsync(
        MobileCallerContext caller, LeaveRequestListQuery query, CancellationToken ct)
    {
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);

        var requests = await leave.QueryRequestsAsync(
            caller.TenantKey,
            new LeaveRequestFilter(employeeId, null, query.State, query.From, query.To),
            ct);

        var policies = await PoliciesByIdAsync(caller, ct);

        return Page(
            requests
                .OrderByDescending(item => item.SubmittedUtc)
                .Select(item => MapListItem(item, policies))
                .ToList(),
            query.Cursor,
            query.PageSize);
    }

    /// <summary>Every policy, active or not: an old request still shows its type's name and colour.</summary>
    private async Task<IReadOnlyDictionary<long, LeavePolicyRecord>> PoliciesByIdAsync(
        MobileCallerContext caller, CancellationToken ct) =>
        (await leave.GetPoliciesAsync(caller.TenantKey, false, ct)).ToDictionary(item => item.Id);

    public async Task<LeaveRequestDetailDto> GetRequestAsync(
        MobileCallerContext caller, string requestId, CancellationToken ct)
    {
        var request = await RequireRequestAsync(caller, requestId, ct);
        await RequireCanSeeAsync(caller, request, ct);

        var comments = await leave.GetCommentsAsync(caller.TenantKey, request.Id, ct);
        var attachments = Deserialize<List<string>>(request.AttachmentsJson) ?? [];
        var ownEmployeeId = await TryResolveOwnEmployeeIdAsync(caller, ct);

        var isOwner = ownEmployeeId == request.EmployeeId;
        var isPending = request.State == "Pending";

        var actions = new List<ActionAvailabilityDto>
        {
            new("withdraw", isOwner && isPending,
                isOwner
                    ? (isPending ? null : "Only a pending request can be withdrawn.")
                    : "Only the person who applied can withdraw this."),
            new("approve", !isOwner && isPending && caller.Has(ApprovePermission),
                isOwner ? "A request cannot be approved by the person who raised it." : null),
            new("reject", !isOwner && isPending && caller.Has(ApprovePermission),
                isOwner ? "A request cannot be rejected by the person who raised it." : null),
            new("cancel", !isOwner && isPending && caller.Has(ApprovePermission),
                isOwner ? "Withdraw your own request instead of cancelling it." : null),
            new("comment", true, null)
        };

        return new LeaveRequestDetailDto(
            MapListItem(request, await PoliciesByIdAsync(caller, ct)),
            request.Reason,
            attachments.Select(uploadId => new AttachmentMetadataDto(
                uploadId, uploadId, "application/octet-stream", 0, "Attached")).ToList(),
            comments.Select(MapWorkflow).ToList(),
            actions,
            null);
    }

    /// <summary>
    /// Computes chargeable days for a range. The result carries a signed token that the create
    /// call verifies, so the client cannot submit a request claiming fewer days than the rules
    /// actually charge.
    /// </summary>
    public async Task<LeaveCalculationDto> CalculateAsync(
        MobileCallerContext caller, LeaveCalculationRequest request, CancellationToken ct)
    {
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);
        var policy = await RequirePolicyAsync(caller, request.LeaveTypeCode, ct);

        var calculation = await ComputeAsync(
            caller, employeeId, policy,
            request.From, request.To, request.IsFirstDayPartial, request.IsLastDayPartial, ct);

        return calculation;
    }

    public async Task<MutationResultDto> CreateRequestAsync(
        MobileCallerContext caller, CreateLeaveRequest request, CancellationToken ct)
    {
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);
        var policy = await RequirePolicyAsync(caller, request.LeaveTypeCode, ct);

        // Recomputed rather than trusted: the token proves the client saw this answer, and the
        // recomputation proves the answer is still true.
        var calculation = await ComputeAsync(
            caller, employeeId, policy,
            request.From, request.To, request.IsFirstDayPartial, request.IsLastDayPartial, ct);

        if (!VerifyCalculation(
                request.CalculationToken, employeeId, policy.Code, request.From, request.To,
                request.IsFirstDayPartial, request.IsLastDayPartial, calculation.ChargeableDays))
        {
            throw new MobileValidationException(
                "CALCULATION_STALE",
                "The leave calculation has changed since it was shown. Recalculate and submit again.");
        }

        if (calculation.ChargeableDays <= 0)
        {
            throw new MobileValidationException(
                "NO_CHARGEABLE_DAYS",
                "This range charges no leave; every day in it is a holiday or a non-working day.");
        }

        if (policy.DocumentRequiredAboveDays is { } threshold &&
            calculation.ChargeableDays > threshold &&
            request.AttachmentIds.Count == 0)
        {
            throw new MobileValidationException(
                "DOCUMENT_REQUIRED",
                $"{policy.Name} longer than {threshold:0.#} days needs a supporting document.");
        }

        if (!policy.AllowsNegativeBalance && calculation.BalanceAfter < 0)
        {
            throw new MobileValidationException(
                "INSUFFICIENT_BALANCE",
                $"This request needs {calculation.ChargeableDays:0.#} days but only " +
                $"{calculation.BalanceBefore:0.#} are available.");
        }

        var now = DateTimeOffset.UtcNow;
        var days = await BuildChargedDaysAsync(
            caller, employeeId, policy, request.From, request.To,
            request.IsFirstDayPartial, request.IsLastDayPartial, ct);

        var record = new LeaveRequestRecord(
            0, caller.TenantKey, employeeId, policy.Id, policy.Code,
            request.From, request.To, request.IsFirstDayPartial, request.IsLastDayPartial,
            calculation.ChargeableDays, request.Reason.Trim(), "Pending",
            JsonSerializer.Serialize(request.AttachmentIds, Json), now,
            null, null, null, null, null, now);

        var id = await leave.SaveRequestAsync(record, days, ct);

        await leave.AddCommentAsync(new LeaveRequestCommentRecord(
            0, caller.TenantKey, id, "Submitted", caller.UserId, CallerName(caller),
            request.Reason.Trim(), now));

        // Raised after the request is safely stored, so a delivery failure cannot lose it.
        // Until an approver-resolution service exists this addresses everyone in the tenant
        // holding the approve permission, which is wider than the one approver who owns it.
        var applicant = await employees.DescribeAsync(caller.TenantKey, employeeId, ct);

        await notifications.PublishToPermissionAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                NotificationCategory,
                "Leave request submitted",
                $"{applicant.DisplayName} requested {calculation.ChargeableDays:0.#} " +
                $"{DayWord(calculation.ChargeableDays)} of {policy.Name} " +
                $"from {request.From:d MMM} to {request.To:d MMM}.",
                $"/leave/requests/{id}",
                "Normal",
                NotificationSubject,
                id.ToString(CultureInfo.InvariantCulture)),
            ApprovePermission,
            ct);

        return new MutationResultDto(
            id.ToString(CultureInfo.InvariantCulture), "Pending", null, now,
            calculation.Warnings.Count == 0
                ? null
                : string.Join(" ", calculation.Warnings));
    }

    public async Task<MutationResultDto> WithdrawAsync(
        MobileCallerContext caller, string requestId, WithdrawLeaveRequest request, CancellationToken ct)
    {
        var existing = await RequireRequestAsync(caller, requestId, ct);
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);

        if (existing.EmployeeId != employeeId)
        {
            throw new MobileValidationException(
                "NOT_OWNER", "Only the person who applied can withdraw this request.");
        }

        if (existing.State != "Pending")
        {
            throw new MobileValidationException(
                "NOT_PENDING", $"This request is already {existing.State.ToLowerInvariant()}.");
        }

        var now = DateTimeOffset.UtcNow;
        var updated = existing with { State = "Withdrawn", UpdatedUtc = now };
        await leave.UpdateRequestStateAsync(updated, ct);

        await leave.AddCommentAsync(new LeaveRequestCommentRecord(
            0, caller.TenantKey, existing.Id, "Withdrawn", caller.UserId, CallerName(caller),
            request.Comment, now));

        // Approvers are told, so nobody works a queue item that no longer needs a decision.
        var applicant = await employees.DescribeAsync(caller.TenantKey, existing.EmployeeId, ct);

        await notifications.PublishToPermissionAsync(
            new ITF.Shared.NotificationRequest(
                caller.TenantKey,
                NotificationCategory,
                "Leave request withdrawn",
                $"{applicant.DisplayName} withdrew their leave from " +
                $"{existing.FromDate:d MMM} to {existing.ToDate:d MMM}. No decision is needed.",
                $"/leave/requests/{existing.Id}",
                "Low",
                NotificationSubject,
                $"{existing.Id}:withdrawn"),
            ApprovePermission,
            ct);

        return new MutationResultDto(requestId, "Withdrawn", null, now, null);
    }

    public async Task<MutationResultDto> CommentAsync(
        MobileCallerContext caller, string requestId, string comment, CancellationToken ct)
    {
        var request = await RequireRequestAsync(caller, requestId, ct);
        await RequireCanSeeAsync(caller, request, ct);

        if (string.IsNullOrWhiteSpace(comment))
        {
            throw new MobileValidationException("COMMENT_REQUIRED", "A comment cannot be empty.");
        }

        var now = DateTimeOffset.UtcNow;
        await leave.AddCommentAsync(new LeaveRequestCommentRecord(
            0, caller.TenantKey, request.Id, "Commented", caller.UserId, CallerName(caller),
            comment.Trim(), now));

        return new MutationResultDto(requestId, request.State, null, now, null);
    }

    // ---- Team -------------------------------------------------------------

    public async Task<CursorPage<LeaveRequestListItemDto>> GetTeamRequestsAsync(
        MobileCallerContext caller, TeamLeaveQuery query, CancellationToken ct)
    {
        Require(caller, TeamPermission);

        var reports = await ResolveReportsAsync(caller, ct);
        if (reports.Count == 0)
        {
            return Empty<LeaveRequestListItemDto>(query.PageSize);
        }

        // The approval queue is the pending subset, so a manager opening it sees work to do
        // rather than history.
        var state = query.AwaitingMyDecisionOnly ? "Pending" : query.State;

        var requests = await leave.QueryRequestsAsync(
            caller.TenantKey,
            new LeaveRequestFilter(null, reports, state, query.From, query.To),
            ct);

        var policies = await PoliciesByIdAsync(caller, ct);

        return Page(requests.Select(item => MapListItem(item, policies)).ToList(), query.Cursor, query.PageSize);
    }

    public async Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller, string requestId, LeaveDecisionRequest request, CancellationToken ct)
    {
        Require(caller, ApprovePermission);

        var existing = await RequireRequestAsync(caller, requestId, ct);

        if (existing.State != "Pending")
        {
            throw new MobileValidationException(
                "NOT_PENDING", $"This request is already {existing.State.ToLowerInvariant()}.");
        }

        var ownEmployeeId = await TryResolveOwnEmployeeIdAsync(caller, ct);
        if (ownEmployeeId == existing.EmployeeId)
        {
            throw new MobileValidationException(
                "SELF_APPROVAL", "A leave request cannot be decided by the person who raised it.");
        }

        var decision = request.Decision?.Trim().ToLowerInvariant();
        var state = decision switch
        {
            "approve" => "Approved",
            "reject" => "Rejected",

            // "Cancel Request" on the approver's card (phase C, Q1): the request is closed
            // without being judged — no balance is spent, and the employee is told why.
            "cancel" => "Cancelled",
            _ => throw new MobileValidationException(
                "DECISION_INVALID", "A decision is approve, reject or cancel.")
        };

        if (state is "Rejected" or "Cancelled" && string.IsNullOrWhiteSpace(request.Comment))
        {
            throw new MobileValidationException(
                "REASON_REQUIRED",
                state == "Rejected"
                    ? "A rejection has to say why, so the employee knows what to do next."
                    : "A cancellation has to say why, so the employee knows what happened.");
        }

        // Balance is checked again at approval: other requests may have been approved since
        // this one was raised, and the approval is what actually spends the balance.
        if (state == "Approved")
        {
            var policy = await leave.GetPolicyAsync(caller.TenantKey, existing.PolicyId, ct);

            if (policy is { AllowsNegativeBalance: false })
            {
                var available = await AvailableAsync(
                    caller, existing.EmployeeId, policy, existing.FromDate.Year, ct);

                // The request's own pending days are already counted in available, so they are
                // added back before comparing.
                if (available + existing.Days < existing.Days)
                {
                    throw new MobileValidationException(
                        "INSUFFICIENT_BALANCE",
                        "The balance no longer covers this request. It has to be rejected or the balance adjusted.");
                }
            }
        }

        var now = DateTimeOffset.UtcNow;
        var updated = existing with
        {
            State = state,
            DecidedByUserId = caller.UserId,
            DecidedByName = CallerName(caller),
            DecidedUtc = now,
            DecisionReasonCode = request.ReasonCode,
            DecisionComment = request.Comment,
            UpdatedUtc = now
        };

        await leave.UpdateRequestStateAsync(updated, ct);

        await leave.AddCommentAsync(new LeaveRequestCommentRecord(
            0, caller.TenantKey, existing.Id, state, caller.UserId, CallerName(caller),
            request.Comment, now));

        // The person who asked is told the outcome. A rejection carries its reason, because
        // the whole point of requiring one is that the employee reads it.
        var applicant = await employees.DescribeAsync(caller.TenantKey, existing.EmployeeId, ct);

        if (applicant.AccountId is { Length: > 0 } recipient)
        {
            var body = state == "Approved"
                ? $"Your leave from {existing.FromDate:d MMM} to {existing.ToDate:d MMM} " +
                  $"({existing.Days:0.#} {DayWord(existing.Days)}) was approved."
                : $"Your leave from {existing.FromDate:d MMM} to {existing.ToDate:d MMM} was " +
                  $"{state.ToLowerInvariant()}. {request.Comment?.Trim()}".TrimEnd();

            await notifications.PublishAsync(
                new ITF.Shared.NotificationRequest(
                    caller.TenantKey,
                    NotificationCategory,
                    $"Leave {state.ToLowerInvariant()}",
                    body,
                    $"/leave/requests/{existing.Id}",
                    state == "Approved" ? "Normal" : "High",
                    NotificationSubject,
                    $"{existing.Id}:{state.ToLowerInvariant()}"),
                [recipient],
                ct);
        }

        return new MutationResultDto(requestId, state, null, now, null);
    }

    public async Task<LeaveCalendarDto> GetTeamCalendarAsync(
        MobileCallerContext caller, TeamLeaveQuery query, CancellationToken ct)
    {
        Require(caller, TeamPermission);

        var from = query.From ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var to = query.To ?? from.AddDays(30);
        var reports = await ResolveReportsAsync(caller, ct);
        var holidays = await leave.GetHolidaysAsync(caller.TenantKey, from, to, null, ct);

        if (reports.Count == 0)
        {
            return new LeaveCalendarDto(
                new DateRangeDto(from, to, "UTC"), [], holidays.Select(item => item.Date).ToList());
        }

        var requests = await leave.QueryRequestsAsync(
            caller.TenantKey,
            new LeaveRequestFilter(null, reports, query.State, from, to),
            ct);

        var live = requests.Where(item => item.State is "Pending" or "Approved").ToList();
        var days = await leave.GetDaysForRequestsAsync(
            caller.TenantKey, live.Select(item => item.Id).ToList(), ct);

        var staff = await employees.QueryAsync(caller.TenantKey, new EmployeeQueryFilter(null, null, null, null, null), ct);
        var byId = staff.ToDictionary(item => item.Id);
        var byRequest = live.ToDictionary(item => item.Id);

        var entries = new List<LeaveCalendarEntryDto>();

        foreach (var day in days.Where(item => item.Date >= from && item.Date <= to))
        {
            if (!byRequest.TryGetValue(day.RequestId, out var request))
            {
                continue;
            }

            var person = byId.GetValueOrDefault(request.EmployeeId);

            entries.Add(new LeaveCalendarEntryDto(
                day.Date,
                new PersonReferenceDto(
                    request.EmployeeId.ToString(CultureInfo.InvariantCulture),
                    person is null ? "Employee" : $"{person.LegalFirstName} {person.LegalLastName}".Trim(),
                    person?.AvatarUrl,
                    person?.EmployeeNumber),
                request.LeaveTypeCode,
                request.LeaveTypeCode,
                day.Portion,
                StatusFor(request.State)));
        }

        return new LeaveCalendarDto(
            new DateRangeDto(from, to, "UTC"),
            entries.OrderBy(item => item.Date).ToList(),
            holidays.Select(item => item.Date).ToList());
    }

    // ---- Credits and adjustments ------------------------------------------

    public async Task<IReadOnlyList<LeaveAdjustmentCreditDto>> GetAdjustmentCreditsAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);
        var credits = await leave.GetCreditsAsync(caller.TenantKey, employeeId, true, ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // An expired credit cannot be encashed, so it is not offered.
        return credits
            .Where(item => item.ExpiresOn >= today)
            .Select(item => new LeaveAdjustmentCreditDto(
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.SourceCode,
                item.EarnedOn,
                item.ExpiresOn,
                item.Days,
                item.IsEncashable,
                item.EncashmentAmount is null
                    ? null
                    : new MoneyDto(item.EncashmentAmount.Value, item.EncashmentCurrency ?? "INR")))
            .ToList();
    }

    public async Task<MutationResultDto> CreateAdjustmentAsync(
        MobileCallerContext caller, LeaveAdjustmentRequest request, CancellationToken ct)
    {
        var employeeId = await RequireOwnEmployeeIdAsync(caller, ct);

        var kind = request.Kind?.Trim().ToLowerInvariant() switch
        {
            "encash" => "Encash",
            "carryforward" => "CarryForward",
            _ => throw new MobileValidationException(
                "KIND_INVALID", "An adjustment is either encash or carryForward.")
        };

        if (request.CreditIds.Count == 0)
        {
            throw new MobileValidationException(
                "CREDITS_REQUIRED", "Select at least one credit to adjust.");
        }

        var ids = new List<long>();
        foreach (var raw in request.CreditIds)
        {
            if (!long.TryParse(raw, out var creditId))
            {
                throw new MobileValidationException("CREDIT_INVALID", $"{raw} is not a credit id.");
            }

            ids.Add(creditId);
        }

        var credits = await leave.GetCreditsByIdAsync(caller.TenantKey, ids, ct);

        if (credits.Count != ids.Count)
        {
            throw new MobileValidationException(
                "CREDIT_NOT_FOUND", "One of the selected credits no longer exists.");
        }

        // Ownership, consumption and expiry are all checked here rather than trusted from the
        // list call, because the list was rendered at some earlier moment.
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var credit in credits)
        {
            if (credit.EmployeeId != employeeId)
            {
                throw new MobileValidationException(
                    "CREDIT_NOT_YOURS", "One of the selected credits belongs to someone else.");
            }

            if (credit.ConsumedByAdjustmentId is not null)
            {
                throw new MobileValidationException(
                    "CREDIT_CONSUMED", "One of the selected credits has already been adjusted.");
            }

            if (credit.ExpiresOn < today)
            {
                throw new MobileValidationException(
                    "CREDIT_EXPIRED", "One of the selected credits has expired.");
            }

            if (kind == "Encash" && !credit.IsEncashable)
            {
                throw new MobileValidationException(
                    "CREDIT_NOT_ENCASHABLE", "One of the selected credits cannot be encashed.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var totalDays = credits.Sum(item => item.Days);
        var amount = kind == "Encash" ? credits.Sum(item => item.EncashmentAmount ?? 0m) : (decimal?)null;
        var currency = credits.FirstOrDefault(item => item.EncashmentCurrency is not null)?.EncashmentCurrency;

        var adjustment = new LeaveAdjustmentRecord(
            0, caller.TenantKey, employeeId, kind,
            JsonSerializer.Serialize(ids, Json), totalDays, amount, currency,
            "Pending", request.Comment, now, null);

        var id = await leave.AddAdjustmentAsync(adjustment, ids, ct);

        return new MutationResultDto(
            id.ToString(CultureInfo.InvariantCulture), "Pending", null, now, null);
    }

    // ---- Policies ---------------------------------------------------------

    public async Task<IReadOnlyList<LeavePolicySummaryDto>> ListAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var policies = await leave.GetPoliciesAsync(caller.TenantKey, true, ct);
        return policies.Select(MapPolicySummary).ToList();
    }

    public async Task<LeavePolicyDetailDto> GetAsync(
        MobileCallerContext caller, string policyId, int? year, CancellationToken ct)
    {
        if (!long.TryParse(policyId, out var id))
        {
            throw new MobileNotFoundException($"Leave policy {policyId} was not found.");
        }

        var policy = await leave.GetPolicyAsync(caller.TenantKey, id, ct)
                     ?? throw new MobileNotFoundException($"Leave policy {policyId} was not found.");

        var documents = Deserialize<List<string>>(policy.DocumentsJson) ?? [];

        return new LeavePolicyDetailDto(
            MapPolicySummary(policy),
            policy.EligibilityText,
            policy.AccrualRuleText,
            policy.CarryForwardRuleText,
            policy.NoticeRuleText,
            documents);
    }

    // ---- Calculation ------------------------------------------------------

    /// <summary>
    /// Works out what a range actually charges: weekends and holidays come out, half days are
    /// halved, and days already claimed by another live request are refused outright.
    /// </summary>
    private async Task<LeaveCalculationDto> ComputeAsync(
        MobileCallerContext caller,
        long employeeId,
        LeavePolicyRecord policy,
        DateOnly from,
        DateOnly to,
        bool firstPartial,
        bool lastPartial,
        CancellationToken ct)
    {
        if (to < from)
        {
            throw new MobileValidationException(
                "RANGE_INVALID", "The last day of leave cannot come before the first.");
        }

        if ((firstPartial || lastPartial) && !policy.AllowsHalfDay)
        {
            throw new MobileValidationException(
                "HALF_DAY_NOT_ALLOWED", $"{policy.Name} cannot be taken as a half day.");
        }

        var overlaps = await leave.GetClaimedDaysAsync(caller.TenantKey, employeeId, from, to, null, ct);
        if (overlaps.Count > 0)
        {
            var first = overlaps.OrderBy(item => item.Date).First().Date;
            throw new MobileValidationException(
                "OVERLAPPING_LEAVE",
                $"You already have leave applied for {first:dd MMM yyyy}.");
        }

        var employee = await employees.GetAsync(caller.TenantKey, employeeId, ct);
        var holidays = await leave.GetHolidaysAsync(
            caller.TenantKey, from, to, employee?.LocationCode, ct);
        var holidayDates = holidays.Select(item => item.Date).ToHashSet();

        var excluded = new List<DateOnly>();
        var warnings = new List<string>();
        var chargeable = 0m;
        var requested = 0m;

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            requested += 1m;

            var isNonWorking = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ||
                               holidayDates.Contains(date);

            if (isNonWorking && !policy.CountsNonWorkingDays)
            {
                excluded.Add(date);
                continue;
            }

            var portion = 1m;
            if (date == from && firstPartial)
            {
                portion = 0.5m;
            }

            if (date == to && lastPartial)
            {
                portion = 0.5m;
            }

            chargeable += portion;
        }

        var available = await AvailableAsync(caller, employeeId, policy, from.Year, ct);

        var noticeGiven = from.DayNumber - DateOnly.FromDateTime(DateTime.UtcNow).DayNumber;
        if (policy.NoticeDays > 0 && noticeGiven < policy.NoticeDays)
        {
            warnings.Add(
                $"{policy.Name} normally needs {policy.NoticeDays} days of notice; this gives {Math.Max(noticeGiven, 0)}.");
        }

        if (available - chargeable < 0)
        {
            warnings.Add(
                policy.AllowsNegativeBalance
                    ? "This takes the balance below zero."
                    : "There is not enough balance for this request.");
        }

        var token = SignCalculation(
            employeeId, policy.Code, from, to, firstPartial, lastPartial, chargeable,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        return new LeaveCalculationDto(
            requested, chargeable, available, available - chargeable, excluded, warnings, token);
    }

    private async Task<IReadOnlyList<LeaveRequestDayRecord>> BuildChargedDaysAsync(
        MobileCallerContext caller,
        long employeeId,
        LeavePolicyRecord policy,
        DateOnly from,
        DateOnly to,
        bool firstPartial,
        bool lastPartial,
        CancellationToken ct)
    {
        var employee = await employees.GetAsync(caller.TenantKey, employeeId, ct);
        var holidays = await leave.GetHolidaysAsync(caller.TenantKey, from, to, employee?.LocationCode, ct);
        var holidayDates = holidays.Select(item => item.Date).ToHashSet();

        var days = new List<LeaveRequestDayRecord>();

        for (var date = from; date <= to; date = date.AddDays(1))
        {
            var isNonWorking = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ||
                               holidayDates.Contains(date);

            if (isNonWorking && !policy.CountsNonWorkingDays)
            {
                continue;
            }

            var portion = 1m;
            if (date == from && firstPartial)
            {
                portion = 0.5m;
            }

            if (date == to && lastPartial)
            {
                portion = 0.5m;
            }

            days.Add(new LeaveRequestDayRecord(0, caller.TenantKey, 0, employeeId, date, portion));
        }

        return days;
    }

    private async Task<decimal> AvailableAsync(
        MobileCallerContext caller, long employeeId, LeavePolicyRecord policy, int year, CancellationToken ct)
    {
        var balance = await leave.GetBalanceAsync(caller.TenantKey, employeeId, policy.Id, year, ct);
        var consumption = await leave.GetConsumptionAsync(caller.TenantKey, employeeId, year, ct);
        var used = consumption.FirstOrDefault(item => item.PolicyId == policy.Id);

        var granted = (balance?.Accrued ?? 0m) + (balance?.CarriedForward ?? 0m) + (balance?.AdjustmentDays ?? 0m);
        return granted - (used?.UsedDays ?? 0m) - (used?.PendingDays ?? 0m);
    }

    // ---- Calculation token ------------------------------------------------

    private static string SignCalculation(
        long employeeId, string code, DateOnly from, DateOnly to,
        bool firstPartial, bool lastPartial, decimal chargeable, long issued)
    {
        var payload = string.Create(CultureInfo.InvariantCulture,
            $"{employeeId}|{code}|{from:yyyy-MM-dd}|{to:yyyy-MM-dd}|{firstPartial}|{lastPartial}|{chargeable}|{issued}");

        var signature = Convert.ToHexString(
            HMACSHA256.HashData(CalculationKey, Encoding.UTF8.GetBytes(payload)));

        return $"{issued}.{signature}";
    }

    /// <summary>
    /// Accepts a token only when it signs exactly these inputs and is still inside its window.
    /// The signature is recomputed from the server's own numbers using the timestamp the client
    /// presented, so a client that edited the range, the half-day flags or the day count after
    /// calculating produces a signature that cannot match.
    /// </summary>
    private static bool VerifyCalculation(
        string? presented, long employeeId, string code, DateOnly from, DateOnly to,
        bool firstPartial, bool lastPartial, decimal chargeable)
    {
        if (string.IsNullOrWhiteSpace(presented))
        {
            return false;
        }

        var parts = presented.Split('.');
        if (parts.Length != 2 || !long.TryParse(parts[0], CultureInfo.InvariantCulture, out var issued))
        {
            return false;
        }

        var age = DateTimeOffset.UtcNow - DateTimeOffset.FromUnixTimeSeconds(issued);

        // A token from the future is as suspect as an expired one; a minute absorbs clock skew.
        if (age > CalculationLifetime || age < TimeSpan.FromMinutes(-1))
        {
            return false;
        }

        var expected = SignCalculation(
            employeeId, code, from, to, firstPartial, lastPartial, chargeable, issued);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(presented));
    }

    // ---- Helpers ----------------------------------------------------------

    /// <summary>
    /// The name recorded against a comment or decision. The token carries an employee number
    /// rather than a display name, so that is what the trail shows.
    /// </summary>
    private static string CallerName(MobileCallerContext caller) =>
        string.IsNullOrWhiteSpace(caller.EmployeeId) ? caller.UserId : caller.EmployeeId;

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }

    private static string DayWord(decimal days) => days == 1m ? "day" : "days";

    private async Task<long> RequireOwnEmployeeIdAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var employee = await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct);

        return employee?.Id ?? throw new MobileValidationException(
            "NO_EMPLOYEE_RECORD",
            "This account is not linked to an employee record, so it has no leave of its own.");
    }

    private async Task<long?> TryResolveOwnEmployeeIdAsync(MobileCallerContext caller, CancellationToken ct) =>
        (await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct))?.Id;

    /// <summary>
    /// Whose leave a call is about. Asking for someone else needs the team permission, so a
    /// query parameter cannot be used to read a colleague's balance.
    /// </summary>
    private async Task<long> ResolveSubjectAsync(
        MobileCallerContext caller, string? requestedEmployeeId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(requestedEmployeeId))
        {
            return await RequireOwnEmployeeIdAsync(caller, ct);
        }

        if (!long.TryParse(requestedEmployeeId, out var employeeId))
        {
            throw new MobileNotFoundException($"Employee {requestedEmployeeId} was not found.");
        }

        var own = await TryResolveOwnEmployeeIdAsync(caller, ct);
        if (own == employeeId)
        {
            return employeeId;
        }

        Require(caller, TeamPermission);
        return employeeId;
    }

    private async Task<LeaveRequestRecord> RequireRequestAsync(
        MobileCallerContext caller, string requestId, CancellationToken ct)
    {
        if (!long.TryParse(requestId, out var id))
        {
            throw new MobileNotFoundException($"Leave request {requestId} was not found.");
        }

        return await leave.GetRequestAsync(caller.TenantKey, id, ct)
               ?? throw new MobileNotFoundException($"Leave request {requestId} was not found.");
    }

    private async Task RequireCanSeeAsync(
        MobileCallerContext caller, LeaveRequestRecord request, CancellationToken ct)
    {
        var own = await TryResolveOwnEmployeeIdAsync(caller, ct);

        if (own == request.EmployeeId || caller.Has(TeamPermission) || caller.Has(ApprovePermission))
        {
            return;
        }

        throw new MobileAccessDeniedException(ViewPermission);
    }

    private async Task<LeavePolicyRecord> RequirePolicyAsync(
        MobileCallerContext caller, string code, CancellationToken ct)
    {
        var policy = await leave.GetPolicyByCodeAsync(caller.TenantKey, code, ct)
                     ?? throw new MobileNotFoundException($"Leave policy {code} was not found.");

        if (!policy.IsActive)
        {
            throw new MobileValidationException(
                "POLICY_INACTIVE", $"{policy.Name} is no longer available.");
        }

        return policy;
    }

    /// <summary>
    /// The people a manager may act for. Falls back to the whole tenant for an HR account with
    /// no direct reports, because HR approves across the organisation rather than down a line.
    /// </summary>
    private async Task<IReadOnlyList<long>> ResolveReportsAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        var managerId = await TryResolveOwnEmployeeIdAsync(caller, ct);

        if (managerId is not null)
        {
            var reports = await employees.QueryAsync(
                caller.TenantKey, new EmployeeQueryFilter(null, null, null, null, managerId), ct);

            if (reports.Count > 0)
            {
                return reports.Select(item => item.Id).ToList();
            }
        }

        if (!caller.Has(ApprovePermission))
        {
            return [];
        }

        var all = await employees.QueryAsync(
            caller.TenantKey, new EmployeeQueryFilter(null, null, null, null, null), ct);

        return all.Select(item => item.Id).ToList();
    }

    private static LeaveRequestListItemDto MapListItem(LeaveRequestRecord request) =>
        MapListItem(request, new Dictionary<long, LeavePolicyRecord>());

    private static LeaveRequestListItemDto MapListItem(
        LeaveRequestRecord request, IReadOnlyDictionary<long, LeavePolicyRecord> policies)
    {
        policies.TryGetValue(request.PolicyId, out var policy);

        return new LeaveRequestListItemDto(
            request.Id.ToString(CultureInfo.InvariantCulture),
            request.LeaveTypeCode,
            request.FromDate,
            request.ToDate,
            request.Days,
            StatusFor(request.State),
            request.SubmittedUtc,
            policy?.Name ?? request.LeaveTypeCode,
            LeaveTones.Normalize(policy?.ToneKey),
            policy?.Emoji ?? LeaveTones.DefaultEmoji);
    }

    private static LeavePolicySummaryDto MapPolicySummary(LeavePolicyRecord policy) => new(
        policy.Id.ToString(CultureInfo.InvariantCulture),
        policy.Code,
        policy.Name,
        policy.Unit,
        policy.Version,
        LeaveTones.Normalize(policy.ToneKey),
        policy.Emoji,
        policy.AllowsHalfDay);

    private static WorkflowStepDto MapWorkflow(LeaveRequestCommentRecord comment) => new(
        comment.Kind,
        comment.Comment is null ? comment.Kind : $"{comment.Kind}: {comment.Comment}",
        comment.Kind,
        new PersonReferenceDto(comment.AuthorUserId ?? "system", comment.AuthorName),
        null,
        comment.CreatedUtc);

    private static StatusDto StatusFor(string state) => new(
        state,
        state,
        state switch
        {
            "Approved" => "success",
            "Rejected" => "danger",
            "Withdrawn" or "Cancelled" => "neutral",
            _ => "pending"
        },
        DateTimeOffset.UtcNow);

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, Json);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static CursorPage<T> Empty<T>(int pageSize) =>
        new([], null, false, pageSize <= 0 ? 25 : pageSize, 0);

    private static CursorPage<T> Page<T>(IReadOnlyList<T> items, string? cursor, int pageSize)
    {
        var size = pageSize <= 0 ? 25 : Math.Min(pageSize, 100);
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
