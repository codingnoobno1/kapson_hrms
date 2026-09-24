using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

/// <summary>
/// Administration of the rules the leave engine enforces: policies and the holiday calendar.
/// </summary>
public interface ILeaveConfigurationService
{
    Task<IReadOnlyList<LeavePolicyAdminDto>> GetPoliciesAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    Task<long> SavePolicyAsync(
        LeavePolicyAdminDto policy, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveHolidayAdminDto>> GetHolidaysAsync(
        string tenantKey, int year, CancellationToken cancellationToken = default);

    Task<long> SaveHolidayAsync(
        LeaveHolidayAdminDto holiday, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a holiday. Leave already requested keeps the days it was charged, because those
    /// dates were materialised when the request was raised.
    /// </summary>
    Task DeleteHolidayAsync(
        string tenantKey, long holidayId, CancellationToken cancellationToken = default);
}
