using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Employee.Controllers;

/// <summary>
/// The numbers behind the employee Home and Attendance screens.
///
/// Everything here is about the caller's own record. There is no employee id parameter by
/// design: reading someone else's attendance goes through the team endpoints, which carry the
/// permission checks for it.
/// </summary>
[Route("api/mobile/v1/me")]
public sealed class EmployeeKpiController(IEmployeeKpiService kpis) : MobileApiControllerBase
{
    /// <summary>Identity card and the four Home tiles (EMP-S-05).</summary>
    [HttpGet("summary")]
    [ProducesResponseType<ApiEnvelope<EmployeeHomeSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<EmployeeHomeSummaryDto>>> Summary(
        CancellationToken ct) =>
        OkEnvelope(await kpis.GetHomeSummaryAsync(Caller, ct));

    /// <summary>The Home Interview Schedule card — interviewers only (EMP-010, D3).</summary>
    [HttpGet("interviews")]
    [ProducesResponseType<ApiEnvelope<EmployeeInterviewScheduleDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<EmployeeInterviewScheduleDto>>> Interviews(
        CancellationToken ct) =>
        OkEnvelope(await kpis.GetMyInterviewsAsync(Caller, ct));

    /// <summary>Present, absent, half-day and holiday counts for one month (EMP-S-32).</summary>
    [HttpGet("kpis/attendance")]
    [ProducesResponseType<ApiEnvelope<AttendanceKpiSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<AttendanceKpiSummaryDto>>> AttendanceKpis(
        [FromQuery] AttendanceKpiQuery query,
        CancellationToken ct) =>
        OkEnvelope(await kpis.GetAttendanceKpisAsync(Caller, query, ct));

    /// <summary>Available balance per leave policy (EMP-S-27).</summary>
    [HttpGet("kpis/leave")]
    [ProducesResponseType<ApiEnvelope<LeaveKpiSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<LeaveKpiSummaryDto>>> LeaveKpis(
        [FromQuery] LeaveKpiQuery query,
        CancellationToken ct) =>
        OkEnvelope(await kpis.GetLeaveKpisAsync(Caller, query, ct));
}
