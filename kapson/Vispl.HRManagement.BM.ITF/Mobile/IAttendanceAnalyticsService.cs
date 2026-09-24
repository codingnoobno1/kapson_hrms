using Vispl.HRManagement.Contracts.Mobile.Home;

namespace Vispl.HRManagement.BM.ITF.Mobile;

/// <summary>
/// The employee's own Attendance Analytics, Export Report and Email Analytics. Caller-scoped:
/// there is no employee id parameter.
/// </summary>
public interface IAttendanceAnalyticsService
{
    Task<AttendanceAnalyticsDto> GetAnalyticsAsync(MobileCallerContext caller, AttendanceAnalyticsQuery query, CancellationToken ct);

    Task<AttendanceReportFileDto> BuildReportAsync(MobileCallerContext caller, AttendanceAnalyticsQuery query, CancellationToken ct);

    Task<AttendanceReportEmailDto> EmailReportAsync(MobileCallerContext caller, AttendanceAnalyticsQuery query, CancellationToken ct);
}
