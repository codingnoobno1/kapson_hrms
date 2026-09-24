using Vispl.HRManagement.Contracts.Mobile.Dashboard;

namespace Vispl.HRManagement.BM.ITF.Mobile;

/// <summary>
/// The dashboard's tables: late arrivals, missing punches and the leave balance grids.
///
/// Separate from <see cref="IHrDashboardService"/> because these are drill-downs — paged,
/// sortable, exportable — rather than the summary the page opens with.
/// </summary>
public interface IWorkforceReportService
{
    Task<DashboardScopeOptionsDto> GetScopeOptionsAsync(
        MobileCallerContext caller, CancellationToken ct);

    Task<LateArrivalReportDto> GetLateArrivalsAsync(
        MobileCallerContext caller, WorkforceReportQuery query, CancellationToken ct);

    Task<MissPunchReportDto> GetMissingPunchesAsync(
        MobileCallerContext caller, WorkforceReportQuery query, CancellationToken ct);

    Task<EmployeeLeaveBalanceGridDto> GetEmployeeLeaveBalancesAsync(
        MobileCallerContext caller, LeaveBalanceGridQuery query, CancellationToken ct);

    Task<DepartmentLeaveBalanceGridDto> GetDepartmentLeaveBalancesAsync(
        MobileCallerContext caller, LeaveBalanceGridQuery query, CancellationToken ct);

    /// <summary>Every opening with the people who have applied to it.</summary>
    Task<OpeningPipelineDto> GetOpeningPipelineAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct);

    /// <summary>
    /// The client's whole leave approval queue, not only the reader's direct reports. Needs
    /// LEAVE.APPROVE rather than a report permission: this is a work queue, and the person who
    /// sees it is the person expected to clear it.
    /// </summary>
    Task<LeaveApprovalQueueDto> GetLeaveApprovalQueueAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct);

    Task<LeaveUsageByDateReportDto> GetLeaveByDateAsync(
        MobileCallerContext caller, MonthlyReportQuery query, CancellationToken ct);

    Task<LeaveTypeUsageReportDto> GetLeaveByTypeAsync(
        MobileCallerContext caller, MonthlyReportQuery query, CancellationToken ct);
}
