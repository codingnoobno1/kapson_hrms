using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

public interface IMasterDataService
{
    Task<IReadOnlyList<MasterOptionDto>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MasterOptionDto>> GetActiveAsync(string tenantKey, string category, CancellationToken cancellationToken = default);
    Task<long> SaveAsync(MasterOptionDto option, CancellationToken cancellationToken = default);
}
