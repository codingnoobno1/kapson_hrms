using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Operations;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// One queue for everything waiting on the caller's decision.
///
/// The inbox is a view over each module's own pending rows rather than a workflow table of its
/// own: a decision recorded in two places is a decision that can disagree with itself. Leave is
/// the only module with an approval state today, so it is the only source; the identifier
/// carries its module so a future source cannot collide with it.
/// </summary>
public sealed class ApprovalService(
    ILeaveRepository leave,
    IEmployeeRepository employees,
    IMobileLeaveService leaveService) : IApprovalService
{
    private const string LeavePrefix = "leave:";
    private const string ApprovePermission = "LEAVE.APPROVE";
    private const string PendingState = "Submitted";

    public async Task<CursorPage<ApprovalListItemDto>> GetInboxAsync(
        MobileCallerContext caller, ApprovalInboxQuery query, CancellationToken ct)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        // Nothing is waiting on someone who cannot decide anything. An empty page is the honest
        // answer rather than a denial: the screen is legitimately reachable, just empty.
        if (!caller.Has(ApprovePermission) ||
            (query.WorkflowType is { } wanted && !string.Equals(wanted, "leave", StringComparison.OrdinalIgnoreCase)))
        {
            return new CursorPage<ApprovalListItemDto>([], null, false, pageSize, 0);
        }

        var pending = await leave.QueryRequestsAsync(
            caller.TenantKey, new LeaveRequestFilter(null, null, PendingState, null, null), ct);

        var names = await NamesForAsync(caller, pending.Select(row => row.EmployeeId), ct);

        var items = pending
            .OrderBy(row => row.SubmittedUtc)
            .Select(row => ToListItem(row, names))
            .ToList();

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            items = items
                .Where(item =>
                    item.Subject.Contains(query.Query, StringComparison.OrdinalIgnoreCase) ||
                    item.Requester.DisplayName.Contains(query.Query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var page = items.Take(pageSize).ToList();

        return new CursorPage<ApprovalListItemDto>(
            page, null, items.Count > page.Count, pageSize, items.Count);
    }

    public async Task<ApprovalDetailDto> GetAsync(
        MobileCallerContext caller, string approvalId, CancellationToken ct)
    {
        var request = await LoadLeaveAsync(caller, approvalId, ct);
        var names = await NamesForAsync(caller, [request.EmployeeId], ct);
        var isPending = string.Equals(request.State, PendingState, StringComparison.OrdinalIgnoreCase);

        var reason = isPending ? null : $"This request is already {request.State.ToLowerInvariant()}.";

        return new ApprovalDetailDto(
            ToListItem(request, names),
            // Leave is a new booking rather than an edit, so there is no prior value to show.
            BeforeValues: new Dictionary<string, string?>(),
            AfterValues: new Dictionary<string, string?>
            {
                ["Leave type"] = request.LeaveTypeCode,
                ["From"] = request.FromDate.ToString("yyyy-MM-dd"),
                ["To"] = request.ToDate.ToString("yyyy-MM-dd"),
                ["Days"] = request.Days.ToString("0.##"),
                ["Reason"] = request.Reason
            },
            Evidence: [],
            Workflow:
            [
                new WorkflowStepDto(
                    "submitted", "Submitted", "complete", null, null, request.SubmittedUtc),
                new WorkflowStepDto(
                    "decision", "Decision", isPending ? "current" : "complete",
                    request.DecidedByName is null
                        ? null
                        : new PersonReferenceDto(request.DecidedByUserId ?? "", request.DecidedByName),
                    null,
                    request.DecidedUtc)
            ],
            Actions:
            [
                new ActionAvailabilityDto("approve", isPending, reason),
                new ActionAvailabilityDto("reject", isPending, reason, RequiresOnline: true, RequiresReason: true)
            ],
            RowVersion: null);
    }

    public async Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller, string approvalId, ApprovalDecisionRequest request, CancellationToken ct)
    {
        var leaveRequest = await LoadLeaveAsync(caller, approvalId, ct);

        // The decision is applied by the module that owns the request, so the balance side
        // effects and rules stay in one place rather than being repeated here.
        return await leaveService.DecideAsync(
            caller,
            leaveRequest.Id.ToString(),
            new LeaveDecisionRequest
            {
                Decision = request.Decision,
                Comment = request.Comment,
                RowVersion = request.RowVersion
            },
            ct);
    }

    public Task<IReadOnlyList<ApprovalDelegationDto>> ListDelegationsAsync(
        MobileCallerContext caller, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Approval Delegation", "delegation store", "stand-in resolution");

    public Task<MutationResultDto> SaveDelegationAsync(
        MobileCallerContext caller, SaveApprovalDelegationRequest request, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Approval Delegation", "delegation store", "stand-in resolution");

    public Task RevokeDelegationAsync(
        MobileCallerContext caller, string delegationId, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Approval Delegation", "delegation store", "stand-in resolution");

    // ---- Helpers ----------------------------------------------------------

    private async Task<VO.Workforce.LeaveRequestRecord> LoadLeaveAsync(
        MobileCallerContext caller, string approvalId, CancellationToken ct)
    {
        if (!caller.Has(ApprovePermission))
        {
            throw new MobileAccessDeniedException(ApprovePermission);
        }

        if (!approvalId.StartsWith(LeavePrefix, StringComparison.OrdinalIgnoreCase) ||
            !long.TryParse(approvalId[LeavePrefix.Length..], out var id))
        {
            throw new MobileNotFoundException("That approval no longer exists.");
        }

        var all = await leave.QueryRequestsAsync(
            caller.TenantKey, new LeaveRequestFilter(null, null, null, null, null), ct);

        return all.FirstOrDefault(row => row.Id == id)
            ?? throw new MobileNotFoundException("That approval no longer exists.");
    }

    private async Task<Dictionary<long, string>> NamesForAsync(
        MobileCallerContext caller, IEnumerable<long> employeeIds, CancellationToken ct)
    {
        var names = new Dictionary<long, string>();

        foreach (var id in employeeIds.Distinct())
        {
            var person = await employees.GetAsync(caller.TenantKey, id, ct);
            if (person is not null)
            {
                names[id] = string.IsNullOrWhiteSpace(person.PreferredName)
                    ? $"{person.LegalFirstName} {person.LegalLastName}".Trim()
                    : person.PreferredName;
            }
        }

        return names;
    }

    private static ApprovalListItemDto ToListItem(
        VO.Workforce.LeaveRequestRecord row, IReadOnlyDictionary<long, string> names) => new(
        $"{LeavePrefix}{row.Id}",
        "leave",
        $"{row.LeaveTypeCode} · {row.Days:0.##} day{(row.Days == 1 ? "" : "s")}",
        new PersonReferenceDto(
            row.EmployeeId.ToString(),
            names.TryGetValue(row.EmployeeId, out var name) ? name : "Unknown"),
        row.SubmittedUtc,
        // Leave has no service-level deadline, so nothing is claimed to be due.
        DueUtc: null,
        Priority: "normal",
        Summary: $"{row.FromDate:dd MMM} to {row.ToDate:dd MMM}");
}
