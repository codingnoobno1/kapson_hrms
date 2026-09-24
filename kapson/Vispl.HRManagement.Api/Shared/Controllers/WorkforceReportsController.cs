using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Dashboard;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Shared.Controllers;

/// <summary>
/// Attendance drill-downs behind the dashboard: who arrived late, and whose punches are
/// incomplete. Shared with the HR mobile app.
/// </summary>
[Route("api/mobile/v1/attendance/reports")]
public sealed class AttendanceReportsController(IWorkforceReportService reports)
    : MobileApiControllerBase
{
    [HttpGet("late-arrivals")]
    [RequirePermission("ATTENDANCE.REPORT")]
    [ProducesResponseType<ApiEnvelope<LateArrivalReportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<LateArrivalReportDto>>> LateArrivals(
        [FromQuery] WorkforceReportQuery query,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetLateArrivalsAsync(Caller, query, ct));

    [HttpGet("miss-punch")]
    [RequirePermission("ATTENDANCE.REPORT")]
    [ProducesResponseType<ApiEnvelope<MissPunchReportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<MissPunchReportDto>>> MissingPunches(
        [FromQuery] WorkforceReportQuery query,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetMissingPunchesAsync(Caller, query, ct));
}

/// <summary>
/// The openings board: every vacancy with the people who have applied to it.
/// </summary>
[Route("api/mobile/v1/recruitment/openings")]
public sealed class OpeningPipelineController(IWorkforceReportService reports)
    : MobileApiControllerBase
{
    [HttpGet("pipeline")]
    [RequirePermission("RECRUITMENT.VIEW")]
    [ProducesResponseType<ApiEnvelope<OpeningPipelineDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<OpeningPipelineDto>>> Pipeline(
        [FromQuery] DashboardScopeQuery query,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetOpeningPipelineAsync(Caller, query, ct));
}

/// <summary>
/// The client's leave approval queue. A work list rather than a report, so it carries
/// LEAVE.APPROVE: whoever can see it is expected to clear it.
/// </summary>
[Route("api/mobile/v1/leave/approvals")]
public sealed class LeaveApprovalQueueController(IWorkforceReportService reports)
    : MobileApiControllerBase
{
    [HttpGet]
    [RequirePermission("LEAVE.APPROVE")]
    [ProducesResponseType<ApiEnvelope<LeaveApprovalQueueDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<LeaveApprovalQueueDto>>> Queue(
        [FromQuery] DashboardScopeQuery query,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetLeaveApprovalQueueAsync(Caller, query, ct));
}

/// <summary>
/// Leave reporting behind the dashboard: balance grids and monthly usage by type.
/// </summary>
[Route("api/mobile/v1/leave/analytics")]
public sealed class LeaveAnalyticsController(IWorkforceReportService reports)
    : MobileApiControllerBase
{
    [HttpGet("balances/employees")]
    [RequirePermission("LEAVE.REPORT")]
    [ProducesResponseType<ApiEnvelope<EmployeeLeaveBalanceGridDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<EmployeeLeaveBalanceGridDto>>> EmployeeBalances(
        [FromQuery] LeaveBalanceGridQuery query,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetEmployeeLeaveBalancesAsync(Caller, query, ct));

    [HttpGet("balances/departments")]
    [RequirePermission("LEAVE.REPORT")]
    [ProducesResponseType<ApiEnvelope<DepartmentLeaveBalanceGridDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<DepartmentLeaveBalanceGridDto>>> DepartmentBalances(
        [FromQuery] LeaveBalanceGridQuery query,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetDepartmentLeaveBalancesAsync(Caller, query, ct));

    [HttpGet("by-date")]
    [RequirePermission("LEAVE.REPORT")]
    [ProducesResponseType<ApiEnvelope<LeaveUsageByDateReportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<LeaveUsageByDateReportDto>>> ByDate(
        [FromQuery] MonthlyReportQuery query,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetLeaveByDateAsync(Caller, query, ct));

    [HttpGet("by-type")]
    [RequirePermission("LEAVE.REPORT")]
    [ProducesResponseType<ApiEnvelope<LeaveTypeUsageReportDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<LeaveTypeUsageReportDto>>> ByType(
        [FromQuery] MonthlyReportQuery query,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetLeaveByTypeAsync(Caller, query, ct));
}
