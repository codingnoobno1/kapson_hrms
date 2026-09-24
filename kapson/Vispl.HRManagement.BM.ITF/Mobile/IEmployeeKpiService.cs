using Vispl.HRManagement.Contracts.Mobile.Home;

namespace Vispl.HRManagement.BM.ITF.Mobile;

/// <summary>
/// The numbers on the employee Home and Attendance screens.
///
/// Every figure is derived from the workforce tables at read time rather than stored, so a tile
/// cannot drift away from the punches and requests it summarises.
/// </summary>
public interface IEmployeeKpiService
{
    Task<EmployeeHomeSummaryDto> GetHomeSummaryAsync(
        MobileCallerContext caller, CancellationToken ct);

    Task<AttendanceKpiSummaryDto> GetAttendanceKpisAsync(
        MobileCallerContext caller, AttendanceKpiQuery query, CancellationToken ct);

    Task<LeaveKpiSummaryDto> GetLeaveKpisAsync(
        MobileCallerContext caller, LeaveKpiQuery query, CancellationToken ct);

    /// <summary>Interviews the caller sits on the panel for, last 7 days to next 14.</summary>
    Task<EmployeeInterviewScheduleDto> GetMyInterviewsAsync(
        MobileCallerContext caller, CancellationToken ct);
}
