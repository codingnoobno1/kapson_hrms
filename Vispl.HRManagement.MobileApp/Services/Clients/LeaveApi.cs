using Vispl.HRManagement.Contracts.Mobile.Dashboard;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Mobile.Workforce;

namespace Vispl.HRManagement.MobileApp.Services.Clients;

/// <summary>
/// The leave approval queue and the decision on it. Deciding is the whole reason an approver
/// opens this app on a phone, so it is the one write this client carries.
/// </summary>
public sealed class LeaveApi(HrApiClient client)
{
    private const string BasePath = "api/mobile/v1/leave";

    public Task<ApiResult<LeaveApprovalQueueDto>> GetApprovalQueueAsync(
        CancellationToken cancellationToken = default) =>
        client.GetAsync<LeaveApprovalQueueDto>(
            $"{BasePath}/approvals", cancellationToken: cancellationToken);

    public Task<ApiResult<MutationResultDto>> DecideAsync(
        string requestId,
        bool approve,
        string? comment = null,
        CancellationToken cancellationToken = default) =>
        client.PostAsync<MutationResultDto>(
            $"{BasePath}/requests/{requestId}/decision",
            new LeaveDecisionRequest
            {
                Decision = approve ? "approve" : "reject",
                Comment = comment
            },
            cancellationToken);

    public Task<ApiResult<IReadOnlyList<LeavePolicySummaryDto>>> GetPoliciesAsync(
        CancellationToken cancellationToken = default) =>
        client.GetAsync<IReadOnlyList<LeavePolicySummaryDto>>(
            $"{BasePath}/policies", cancellationToken: cancellationToken);
}
