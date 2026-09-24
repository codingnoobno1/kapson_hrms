using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class GradeSalaryRepository(HrManagementDbContext dbContext) : IGradeSalaryRepository
{
    public async Task<IReadOnlyList<GradeSalaryRecord>> GetAllAsync(
        string tenantKey, CancellationToken cancellationToken = default) =>
        (await dbContext.GradeSalaryStructures.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey)
            .ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<GradeSalaryRecord?> GetAsync(
        string tenantKey, string gradeCode, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.GradeSalaryStructures.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantKey == tenantKey && item.GradeCode == gradeCode, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpsertAsync(GradeSalaryRecord structure, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.GradeSalaryStructures.SingleOrDefaultAsync(
            item => item.TenantKey == structure.TenantKey && item.GradeCode == structure.GradeCode,
            cancellationToken);

        if (entity is null)
        {
            entity = new GradeSalaryEntity
            {
                TenantKey = structure.TenantKey,
                GradeCode = structure.GradeCode
            };

            dbContext.GradeSalaryStructures.Add(entity);
        }

        entity.BasicMonthly = structure.BasicMonthly;
        entity.ComponentsJson = structure.ComponentsJson;
        entity.UpdatedUtc = structure.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static GradeSalaryRecord ToRecord(GradeSalaryEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        GradeCode = entity.GradeCode,
        BasicMonthly = entity.BasicMonthly,
        ComponentsJson = entity.ComponentsJson,
        UpdatedUtc = entity.UpdatedUtc
    };
}
