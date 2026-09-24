using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.DL.ITF.Workforce;

public interface IOnDutyRepository
{
    Task<IReadOnlyList<OnDutyRequestRecord>> QueryAsync(
        string tenantKey, OnDutyFilter filter, CancellationToken cancellationToken = default);

    Task<OnDutyRequestRecord?> GetAsync(
        string tenantKey, long requestId, CancellationToken cancellationToken = default);

    /// <summary>Pending or approved requests overlapping a range — the double-booking check.</summary>
    Task<IReadOnlyList<OnDutyRequestRecord>> GetOverlappingAsync(
        string tenantKey, long employeeId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    Task<long> AddAsync(OnDutyRequestRecord request, CancellationToken cancellationToken = default);

    Task UpdateAsync(OnDutyRequestRecord request, CancellationToken cancellationToken = default);
}
