using Vispl.HRManagement.Contracts.Mobile.Dashboard;

namespace Vispl.HRManagement.BM.ITF.Mobile;

/// <summary>
/// The HR web portal dashboard.
///
/// Every figure is derived at read time from the workforce tables. The composite exists so the
/// first paint is one round trip; the individual reads exist so refreshing one number does not
/// recompute six charts.
/// </summary>
public interface IHrDashboardService
{
    Task<HrDashboardDto> GetDashboardAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct);

    Task<DashboardHeadlineDto> GetHeadlineAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct);

    Task<PeopleMomentsDto> GetPeopleMomentsAsync(
        MobileCallerContext caller, PeopleMomentsQuery query, CancellationToken ct);

    Task<LeaveAnalyticsDto> GetLeaveAnalyticsAsync(
        MobileCallerContext caller, DashboardScopeQuery query, CancellationToken ct);

    Task<AttendanceOverviewDto> GetAttendanceOverviewAsync(
        MobileCallerContext caller, AttendanceOverviewQuery query, CancellationToken ct);

    Task<IReadOnlyList<CompanyEventDto>> GetUpcomingEventsAsync(
        MobileCallerContext caller, DashboardScopeQuery query, int windowDays, CancellationToken ct);

    /// <summary>The people behind one headline tile — what a click on that tile's card lists.</summary>
    Task<DashboardKpiMembersDto> GetKpiMembersAsync(
        MobileCallerContext caller, string key, DashboardScopeQuery query, CancellationToken ct);
}
