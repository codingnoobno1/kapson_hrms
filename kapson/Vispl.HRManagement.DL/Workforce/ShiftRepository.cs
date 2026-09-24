using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.DL.Workforce;

public sealed class ShiftRepository(HrManagementDbContext dbContext) : IShiftRepository
{
    public async Task<IReadOnlyList<ShiftDefinitionRecord>> GetDefinitionsAsync(
        string tenantKey, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = dbContext.ShiftDefinitions.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        if (activeOnly)
        {
            query = query.Where(item => item.IsActive);
        }

        var rows = await query.OrderBy(item => item.Code).ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<ShiftDefinitionRecord?> GetDefinitionAsync(
        string tenantKey, long definitionId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ShiftDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == definitionId, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<ShiftDefinitionRecord?> GetDefaultDefinitionAsync(
        string tenantKey, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ShiftDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey && item.IsDefault && item.IsActive, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<long> UpsertDefinitionAsync(
        ShiftDefinitionRecord definition, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ShiftDefinitions.FirstOrDefaultAsync(
            item => item.TenantKey == definition.TenantKey && item.Code == definition.Code,
            cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (entity is null)
        {
            entity = new ShiftDefinitionEntity
            {
                TenantKey = definition.TenantKey,
                Code = definition.Code,
                CreatedUtc = now
            };
            dbContext.ShiftDefinitions.Add(entity);
        }

        entity.Name = definition.Name;
        entity.StartTime = definition.StartTime;
        entity.EndTime = definition.EndTime;
        entity.BreakMinutes = definition.BreakMinutes;
        entity.GraceMinutes = definition.GraceMinutes;
        entity.WorkingDays = definition.WorkingDays;
        entity.CrossesMidnight = definition.CrossesMidnight;
        entity.IsDefault = definition.IsDefault;
        entity.IsActive = definition.IsActive;
        entity.UpdatedUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);

        // Exactly one default per tenant. Cleared after the save so the new default already has
        // an id, and so a failed save cannot leave a tenant with none at all.
        if (definition.IsDefault)
        {
            var others = await dbContext.ShiftDefinitions
                .Where(item => item.TenantKey == definition.TenantKey &&
                               item.Id != entity.Id && item.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var other in others)
            {
                other.IsDefault = false;
            }

            if (others.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return entity.Id;
    }

    public async Task<IReadOnlyList<ShiftAssignmentRecord>> GetAssignmentsAsync(
        string tenantKey, IReadOnlyList<long> employeeIds, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var rangeStart = from;
        var rangeEnd = to;

        // Any assignment overlapping the window, so a range that started last year and is still
        // open still answers for today.
        var rows = await dbContext.ShiftAssignments
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           employeeIds.Contains(item.EmployeeId) &&
                           item.EffectiveFrom <= rangeEnd &&
                           (item.EffectiveTo == null || item.EffectiveTo >= rangeStart))
            .OrderByDescending(item => item.EffectiveFrom)
            .ThenByDescending(item => item.AssignedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(MapAssignment).ToList();
    }

    public async Task<long> AddAssignmentAsync(
        ShiftAssignmentRecord assignment, CancellationToken cancellationToken = default)
    {
        var entity = new ShiftAssignmentEntity
        {
            TenantKey = assignment.TenantKey,
            EmployeeId = assignment.EmployeeId,
            ShiftDefinitionId = assignment.ShiftDefinitionId,
            EffectiveFrom = assignment.EffectiveFrom,
            EffectiveTo = assignment.EffectiveTo,
            AssignedUtc = assignment.AssignedUtc == default ? DateTimeOffset.UtcNow : assignment.AssignedUtc,
            AssignedBy = assignment.AssignedBy,
            Note = assignment.Note
        };

        dbContext.ShiftAssignments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task RemoveAssignmentAsync(
        string tenantKey, long assignmentId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ShiftAssignments.FirstOrDefaultAsync(
            item => item.TenantKey == tenantKey && item.Id == assignmentId, cancellationToken);

        if (entity is null)
        {
            return;
        }

        dbContext.ShiftAssignments.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ShiftDefinitionRecord Map(ShiftDefinitionEntity entity) => new(
        entity.Id, entity.TenantKey, entity.Code, entity.Name, entity.StartTime, entity.EndTime,
        entity.BreakMinutes, entity.GraceMinutes, entity.WorkingDays, entity.CrossesMidnight,
        entity.IsDefault, entity.IsActive);

    private static ShiftAssignmentRecord MapAssignment(ShiftAssignmentEntity entity) => new(
        entity.Id, entity.TenantKey, entity.EmployeeId, entity.ShiftDefinitionId,
        entity.EffectiveFrom, entity.EffectiveTo, entity.AssignedUtc, entity.AssignedBy, entity.Note);
}
