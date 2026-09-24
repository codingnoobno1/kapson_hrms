using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class MediaAssetRepository(HrManagementDbContext dbContext) : IMediaAssetRepository
{
    public async Task<IReadOnlyList<MediaAssetRecord>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default) =>
        (await dbContext.MediaAssets.AsNoTracking().Where(item => item.TenantKey == tenantKey)
            .OrderByDescending(item => item.CreatedUtc).ToListAsync(cancellationToken)).Select(ToRecord).ToList();

    public async Task<MediaAssetRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MediaAssets.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<long> AddAsync(MediaAssetRecord asset, CancellationToken cancellationToken = default)
    {
        var entity = new MediaAssetEntity
        {
            TenantKey = asset.TenantKey, OriginalFileName = asset.OriginalFileName, StoredFileName = asset.StoredFileName,
            StoragePath = asset.StoragePath, ContentType = asset.ContentType, Length = asset.Length, CreatedUtc = asset.CreatedUtc
        };
        dbContext.MediaAssets.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private static MediaAssetRecord ToRecord(MediaAssetEntity entity) => new()
    {
        Id = entity.Id, TenantKey = entity.TenantKey, OriginalFileName = entity.OriginalFileName,
        StoredFileName = entity.StoredFileName, StoragePath = entity.StoragePath,
        ContentType = entity.ContentType, Length = entity.Length, CreatedUtc = entity.CreatedUtc
    };
}
