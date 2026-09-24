using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface ITenantRepository
{
    Task<IReadOnlyList<TenantRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TenantRecord?> GetByKeyAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<long> UpsertAsync(TenantRecord tenant, CancellationToken cancellationToken = default);
}
