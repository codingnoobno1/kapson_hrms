using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Employee.Controllers;

/// <summary>
/// Attendance Analytics, Export Report and Email Analytics (phase C). The caller's own attendance
/// only; the figures are the same days HR sees on /hr/attendance and the monthly report.
/// </summary>
[Route("api/mobile/v1/me/attendance")]
public sealed class EmployeeAttendanceAnalyticsController(IAttendanceAnalyticsService analytics) : MobileApiControllerBase
{
    [HttpGet("analytics")]
    [ProducesResponseType<ApiEnvelope<AttendanceAnalyticsDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<AttendanceAnalyticsDto>>> Analytics(
        [FromQuery] AttendanceAnalyticsQuery query, CancellationToken ct) =>
        OkEnvelope(await analytics.GetAnalyticsAsync(Caller, query, ct));

    /// <summary>The Export Report workbook (.xlsx, base64). Never cached.</summary>
    [HttpGet("report")]
    [ProducesResponseType<ApiEnvelope<AttendanceReportFileDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<AttendanceReportFileDto>>> Report(
        [FromQuery] AttendanceAnalyticsQuery query, CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await analytics.BuildReportAsync(Caller, query, ct));
    }

    /// <summary>Email Analytics: the summary to the employee's own work address.</summary>
    [HttpPost("report/email")]
    [ProducesResponseType<ApiEnvelope<AttendanceReportEmailDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<AttendanceReportEmailDto>>> EmailReport(
        [FromQuery] AttendanceAnalyticsQuery query, CancellationToken ct) =>
        OkEnvelope(await analytics.EmailReportAsync(Caller, query, ct));
}
