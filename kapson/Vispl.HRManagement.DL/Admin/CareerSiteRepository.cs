using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class CareerSiteRepository(HrManagementDbContext dbContext) : ICareerSiteRepository
{
    public async Task<CareerSiteRecord?> GetAsync(
        string tenantKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CareerSites
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TenantKey == tenantKey,
                cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpsertAsync(
        CareerSiteRecord record,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CareerSites
            .SingleOrDefaultAsync(item => item.TenantKey == record.TenantKey, cancellationToken);

        if (entity is null)
        {
            entity = new CareerSiteEntity { TenantKey = record.TenantKey };
            dbContext.CareerSites.Add(entity);
        }

        entity.DraftJson = record.DraftJson;
        entity.PublishedJson = record.PublishedJson;
        entity.PublishedVersion = record.PublishedVersion;
        entity.PublishedUtc = record.PublishedUtc;
        entity.UpdatedUtc = record.UpdatedUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static CareerSiteRecord ToRecord(CareerSiteEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        DraftJson = entity.DraftJson,
        PublishedJson = entity.PublishedJson,
        PublishedVersion = entity.PublishedVersion,
        PublishedUtc = entity.PublishedUtc,
        UpdatedUtc = entity.UpdatedUtc
    };
}
