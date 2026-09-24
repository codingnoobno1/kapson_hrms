using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class TenantRepository(HrManagementDbContext dbContext) : ITenantRepository
{
    public async Task<IReadOnlyList<TenantRecord>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await dbContext.Tenants.AsNoTracking().OrderBy(item => item.DisplayName).ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<TenantRecord?> GetByKeyAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(item => item.TenantKey == tenantKey, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<long> UpsertAsync(TenantRecord tenant, CancellationToken cancellationToken = default)
    {
        var entity = tenant.Id == 0 ? null : await dbContext.Tenants.SingleOrDefaultAsync(item => item.Id == tenant.Id, cancellationToken);
        if (entity is null) { entity = new TenantEntity(); dbContext.Tenants.Add(entity); }
        entity.TenantKey = tenant.TenantKey;
        entity.DisplayName = tenant.DisplayName;
        entity.PrimaryDomain = tenant.PrimaryDomain;
        entity.TimeZoneId = tenant.TimeZoneId;
        entity.RetirementAgeYears = tenant.RetirementAgeYears;
        entity.HrPortalEnabled = tenant.HrPortalEnabled;
        entity.IsActive = tenant.IsActive;
        entity.CreatedUtc = tenant.CreatedUtc;
        entity.UpdatedUtc = tenant.UpdatedUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private static TenantRecord ToRecord(TenantEntity entity) => new()
    {
        Id = entity.Id, TenantKey = entity.TenantKey, DisplayName = entity.DisplayName,
        PrimaryDomain = entity.PrimaryDomain, TimeZoneId = entity.TimeZoneId, RetirementAgeYears = entity.RetirementAgeYears,
        HrPortalEnabled = entity.HrPortalEnabled, IsActive = entity.IsActive,
        CreatedUtc = entity.CreatedUtc, UpdatedUtc = entity.UpdatedUtc
    };
}
