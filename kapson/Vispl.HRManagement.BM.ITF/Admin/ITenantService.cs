using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

public interface ITenantService
{
    Task<IReadOnlyList<TenantDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TenantDto?> GetByKeyAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<long> SaveAsync(TenantDto tenant, CancellationToken cancellationToken = default);
}
