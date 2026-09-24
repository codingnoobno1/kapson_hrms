using Vispl.HRManagement.Contracts.Mobile.Dashboard;
using Vispl.HRManagement.Contracts.Mobile.Workforce;

namespace Vispl.HRManagement.MobileApp.Services.Clients;

/// <summary>Today's attendance picture, and the two drill-downs behind it.</summary>
public sealed class AttendanceApi(HrApiClient client)
{
    public Task<ApiResult<AttendanceDashboardDto>> GetDashboardAsync(
        DateOnly? date = null, CancellationToken cancellationToken = default) =>
        client.GetAsync<AttendanceDashboardDto>(
            date is null
                ? "api/mobile/v1/attendance/dashboard"
                : $"api/mobile/v1/attendance/dashboard?date={date:yyyy-MM-dd}",
            cancellationToken: cancellationToken);

    public Task<ApiResult<LateArrivalReportDto>> GetLateArrivalsAsync(
        int pageSize = 25, CancellationToken cancellationToken = default) =>
        client.GetAsync<LateArrivalReportDto>(
            $"api/mobile/v1/attendance/reports/late-arrivals?pageSize={pageSize}",
            cancellationToken: cancellationToken);

    public Task<ApiResult<MissPunchReportDto>> GetMissingPunchesAsync(
        int pageSize = 25, CancellationToken cancellationToken = default) =>
        client.GetAsync<MissPunchReportDto>(
            $"api/mobile/v1/attendance/reports/miss-punch?pageSize={pageSize}",
            cancellationToken: cancellationToken);

    public Task<ApiResult<MovementBoardDto>> GetMovementAsync(
        CancellationToken cancellationToken = default) =>
        client.GetAsync<MovementBoardDto>(
            "api/mobile/v1/movement/out", cancellationToken: cancellationToken);
}
