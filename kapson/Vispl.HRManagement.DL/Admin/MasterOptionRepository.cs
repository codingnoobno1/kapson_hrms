using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class MasterOptionRepository(HrManagementDbContext dbContext) : IMasterOptionRepository
{
    public async Task<IReadOnlyList<MasterOptionRecord>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default) =>
        (await dbContext.MasterOptions.AsNoTracking().Where(item => item.TenantKey == tenantKey)
            .OrderBy(item => item.Category).ThenBy(item => item.SortOrder).ThenBy(item => item.Name).ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<IReadOnlyList<MasterOptionRecord>> GetActiveAsync(string tenantKey, string category, CancellationToken cancellationToken = default) =>
        (await dbContext.MasterOptions.AsNoTracking().Where(item => item.TenantKey == tenantKey && item.Category == category && item.IsActive)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name).ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<long> UpsertAsync(MasterOptionRecord option, CancellationToken cancellationToken = default)
    {
        var entity = option.Id == 0 ? null : await dbContext.MasterOptions.SingleOrDefaultAsync(item => item.Id == option.Id, cancellationToken);
        if (entity is null) { entity = new MasterOptionEntity(); dbContext.MasterOptions.Add(entity); }
        entity.TenantKey = option.TenantKey;
        entity.Category = option.Category;
        entity.Code = option.Code;
        entity.Name = option.Name;
        entity.SortOrder = option.SortOrder;
        entity.ParentCode = option.ParentCode;
        entity.Description = option.Description;
        entity.IsActive = option.IsActive;
        entity.UpdatedUtc = option.UpdatedUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private static MasterOptionRecord ToRecord(MasterOptionEntity entity) => new()
    {
        Id = entity.Id, TenantKey = entity.TenantKey, Category = entity.Category, Code = entity.Code,
        Name = entity.Name, SortOrder = entity.SortOrder, ParentCode = entity.ParentCode, Description = entity.Description,
        IsActive = entity.IsActive, UpdatedUtc = entity.UpdatedUtc
    };
}
