using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Dashboard;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Shared.Controllers;

/// <summary>
/// The HR dashboard, shared by the web portal and the HR mobile app.
///
/// The composite endpoint exists so the portal's first paint is one round trip; the individual
/// endpoints exist so refreshing one panel does not recompute the whole page.
/// </summary>
[Route("api/mobile/v1/dashboard")]
public sealed class HrDashboardController(
    IHrDashboardService dashboard,
    IWorkforceReportService reports) : MobileApiControllerBase
{
    /// <summary>The whole page. Sections the caller may not see come back null, not empty.</summary>
    [HttpGet]
    [RequirePermission("DASHBOARD.VIEW")]
    [ProducesResponseType<ApiEnvelope<HrDashboardDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<HrDashboardDto>>> Get(
        [FromQuery] DashboardScopeQuery query,
        CancellationToken ct) =>
        OkEnvelope(await dashboard.GetDashboardAsync(Caller, query, ct));

    /// <summary>Companies and units the caller may filter by — the filter bar's options.</summary>
    [HttpGet("scopes")]
    [RequirePermission("DASHBOARD.VIEW")]
    [ProducesResponseType<ApiEnvelope<DashboardScopeOptionsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<DashboardScopeOptionsDto>>> Scopes(
        CancellationToken ct) =>
        OkEnvelope(await reports.GetScopeOptionsAsync(Caller, ct));

    /// <summary>The six headline tiles alone, for polling without refetching the charts.</summary>
    [HttpGet("headline")]
    [RequirePermission("DASHBOARD.VIEW")]
    [ProducesResponseType<ApiEnvelope<DashboardHeadlineDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<DashboardHeadlineDto>>> Headline(
        [FromQuery] DashboardScopeQuery query,
        CancellationToken ct) =>
        OkEnvelope(await dashboard.GetHeadlineAsync(Caller, query, ct));

    [HttpGet("moments")]
    [RequirePermission("PEOPLE.MOMENTS.VIEW")]
    [ProducesResponseType<ApiEnvelope<PeopleMomentsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<PeopleMomentsDto>>> Moments(
        [FromQuery] PeopleMomentsQuery query,
        CancellationToken ct) =>
        OkEnvelope(await dashboard.GetPeopleMomentsAsync(Caller, query, ct));

    [HttpGet("leave-analytics")]
    [RequirePermission("LEAVE.REPORT")]
    [ProducesResponseType<ApiEnvelope<LeaveAnalyticsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<LeaveAnalyticsDto>>> LeaveAnalytics(
        [FromQuery] DashboardScopeQuery query,
        CancellationToken ct) =>
        OkEnvelope(await dashboard.GetLeaveAnalyticsAsync(Caller, query, ct));

    [HttpGet("attendance-overview")]
    [RequirePermission("ATTENDANCE.REPORT")]
    [ProducesResponseType<ApiEnvelope<AttendanceOverviewDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<AttendanceOverviewDto>>> AttendanceOverview(
        [FromQuery] AttendanceOverviewQuery query,
        CancellationToken ct) =>
        OkEnvelope(await dashboard.GetAttendanceOverviewAsync(Caller, query, ct));

    [HttpGet("events")]
    [RequirePermission("DASHBOARD.VIEW")]
    [ProducesResponseType<ApiEnvelope<IReadOnlyList<CompanyEventDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<CompanyEventDto>>>> Events(
        [FromQuery] DashboardScopeQuery query,
        [FromQuery] int windowDays = 30,
        CancellationToken ct = default) =>
        OkEnvelope(await dashboard.GetUpcomingEventsAsync(Caller, query, windowDays, ct));
}
