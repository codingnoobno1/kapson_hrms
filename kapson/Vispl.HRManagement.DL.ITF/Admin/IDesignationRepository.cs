using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface IDesignationRepository
{
    Task<IReadOnlyList<DesignationRecord>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);

    Task<long> UpsertAsync(DesignationRecord designation, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string tenantKey, long designationId, CancellationToken cancellationToken = default);

    Task SetOrderAsync(string tenantKey, IReadOnlyList<long> orderedIds, CancellationToken cancellationToken = default);
}
