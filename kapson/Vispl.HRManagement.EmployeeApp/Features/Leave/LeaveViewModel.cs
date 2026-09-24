using System.Collections.ObjectModel;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.EmployeeApp.Shared.ViewModels;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Client.Auth;

namespace Vispl.HRManagement.EmployeeApp.Features.Leave;

/// <summary>
/// EMP-014. The Leave tab root: balances, recent requests, and the approver queue for anyone
/// who holds <c>LEAVE.APPROVE</c>.
///
/// This slot was the Projects tab in the mockups. It became Leave when Projects was dropped
/// (employee-app.md §9.1), which also absorbed the balances sheet that was EMP-034.
/// </summary>
public sealed class LeaveViewModel(LeaveApiClient leave, MobileSession session) : ViewModelBase
{
    private bool canApprove;
    private int pendingApprovals;

    public ObservableCollection<LeaveBalanceDto> Balances { get; } = [];

    public ObservableCollection<LeaveRequestListItemDto> Requests { get; } = [];

    /// <summary>Policy code to display name, so a row shows "Casual Leave" rather than "CL".</summary>
    public Dictionary<string, string> PolicyNames { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The one role switch in this app. A permission the token does not carry hides the
    /// approver surface — and the server refuses it regardless, which is the actual control.
    /// </summary>
    public bool CanApprove
    {
        get => canApprove;
        private set => Set(ref canApprove, value);
    }

    public int PendingApprovals
    {
        get => pendingApprovals;
        private set => Set(ref pendingApprovals, value);
    }

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        CanApprove = session.Has("LEAVE.APPROVE");

        await RunAsync(async () =>
        {
            var balances = await leave.GetBalancesAsync(null, ct);
            var requests = await leave.GetMyRequestsAsync(null, ct);

            Balances.Clear();
            foreach (var balance in balances)
            {
                Balances.Add(balance);
                PolicyNames[balance.LeaveTypeCode] = balance.LeaveTypeName;
            }

            Requests.Clear();
            foreach (var request in requests.Items.OrderByDescending(item => item.SubmittedUtc))
            {
                Requests.Add(request);
            }

            if (Balances.Count == 0 && Requests.Count == 0)
            {
                Empty("You have no leave policies assigned yet. HR sets these up.");
                return;
            }

            Ready();
        });

        await RefreshApprovalCountAsync(ct);
    }

    /// <summary>
    /// Counts what is waiting on this person, softly: a manager without team access still gets
    /// a working Leave tab rather than an error over a badge.
    /// </summary>
    private async Task RefreshApprovalCountAsync(CancellationToken ct)
    {
        if (!CanApprove)
        {
            PendingApprovals = 0;
            return;
        }

        try
        {
            var team = await leave.GetTeamRequestsAsync(null, ct);
            PendingApprovals = team.Items.Count(item =>
                string.Equals(item.Status.Code, "Pending", StringComparison.OrdinalIgnoreCase));
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine($"Approval count unavailable: {exception.Message}");
            PendingApprovals = 0;
        }
    }

    public string NameFor(string leaveTypeCode) =>
        PolicyNames.TryGetValue(leaveTypeCode, out var name) ? name : leaveTypeCode;
}
