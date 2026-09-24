using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class DesignationRepository(HrManagementDbContext dbContext) : IDesignationRepository
{
    public async Task<IReadOnlyList<DesignationRecord>> GetAllAsync(
        string tenantKey, CancellationToken cancellationToken = default) =>
        (await dbContext.Designations.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<long> UpsertAsync(DesignationRecord designation, CancellationToken cancellationToken = default)
    {
        var entity = designation.Id == 0
            ? null
            : await dbContext.Designations.SingleOrDefaultAsync(
                item => item.Id == designation.Id && item.TenantKey == designation.TenantKey, cancellationToken);

        if (entity is null)
        {
            entity = new DesignationEntity();
            dbContext.Designations.Add(entity);
        }

        entity.TenantKey = designation.TenantKey;
        entity.Code = designation.Code;
        entity.Name = designation.Name;
        entity.GradeCode = designation.GradeCode;
        entity.DepartmentCode = designation.DepartmentCode;
        entity.Description = designation.Description;
        entity.SortOrder = designation.SortOrder;
        entity.IsActive = designation.IsActive;
        entity.UpdatedUtc = designation.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> DeleteAsync(
        string tenantKey, long designationId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Designations.SingleOrDefaultAsync(
            item => item.TenantKey == tenantKey && item.Id == designationId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        dbContext.Designations.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task SetOrderAsync(
        string tenantKey, IReadOnlyList<long> orderedIds, CancellationToken cancellationToken = default)
    {
        var entities = await dbContext.Designations
            .Where(item => item.TenantKey == tenantKey)
            .OrderBy(item => item.SortOrder).ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        var position = orderedIds
            .Distinct()
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index);

        // OrderBy is stable, so anything not in the list keeps its existing relative order.
        var ordered = entities
            .OrderBy(item => position.TryGetValue(item.Id, out var index) ? index : int.MaxValue)
            .ToList();

        for (var index = 0; index < ordered.Count; index++)
        {
            ordered[index].SortOrder = index + 1;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static DesignationRecord ToRecord(DesignationEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        Code = entity.Code,
        Name = entity.Name,
        GradeCode = entity.GradeCode,
        DepartmentCode = entity.DepartmentCode,
        Description = entity.Description,
        SortOrder = entity.SortOrder,
        IsActive = entity.IsActive,
        UpdatedUtc = entity.UpdatedUtc
    };
}
