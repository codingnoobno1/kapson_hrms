using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface IMasterOptionRepository
{
    Task<IReadOnlyList<MasterOptionRecord>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MasterOptionRecord>> GetActiveAsync(string tenantKey, string category, CancellationToken cancellationToken = default);
    Task<long> UpsertAsync(MasterOptionRecord option, CancellationToken cancellationToken = default);
}
