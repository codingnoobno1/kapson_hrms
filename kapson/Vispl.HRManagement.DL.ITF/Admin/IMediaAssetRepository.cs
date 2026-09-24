using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface IMediaAssetRepository
{
    Task<IReadOnlyList<MediaAssetRecord>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<MediaAssetRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<long> AddAsync(MediaAssetRecord asset, CancellationToken cancellationToken = default);
}
