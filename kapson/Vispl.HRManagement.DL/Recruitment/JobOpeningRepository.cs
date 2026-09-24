using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Recruitment;

namespace Vispl.HRManagement.DL.Recruitment;

public sealed class JobOpeningRepository(HrManagementDbContext dbContext) : IJobOpeningRepository
{
    public async Task<IReadOnlyList<JobOpeningRecord>> GetPublishedAsync(
        string tenantKey,
        CancellationToken cancellationToken = default) =>
        (await dbContext.JobOpenings
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.IsPublished)
            .OrderByDescending(item => item.PublishedUtc)
            .ToListAsync(cancellationToken))
        .Select(ToRecord)
        .ToList();

    public async Task<IReadOnlyList<JobOpeningRecord>> GetAllAsync(
        string tenantKey,
        CancellationToken cancellationToken = default) =>
        (await dbContext.JobOpenings
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey)
            .OrderByDescending(item => item.UpdatedUtc)
            .ToListAsync(cancellationToken))
        .Select(ToRecord)
        .ToList();

    public async Task<JobOpeningRecord?> GetBySlugAsync(
        string tenantKey,
        string slug,
        bool publishedOnly,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.JobOpenings.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.Slug == slug);

        if (publishedOnly)
        {
            query = query.Where(item => item.IsPublished);
        }

        var entity = await query.SingleOrDefaultAsync(cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<JobOpeningRecord?> GetByIdAsync(
        long id,
        CancellationToken cancellationToken = default) =>
        (await dbContext.JobOpenings
            .AsNoTracking()
            .Where(item => item.Id == id)
            .SingleOrDefaultAsync(cancellationToken)) is { } entity
            ? ToRecord(entity)
            : null;

    public async Task<long> UpsertAsync(
        JobOpeningRecord record,
        CancellationToken cancellationToken = default)
    {
        var entity = record.Id == 0
            ? null
            : await dbContext.JobOpenings.SingleOrDefaultAsync(item => item.Id == record.Id, cancellationToken);

        if (entity is null)
        {
            entity = new JobOpeningEntity();
            dbContext.JobOpenings.Add(entity);
        }

        entity.TenantKey = record.TenantKey;
        entity.Slug = record.Slug;
        entity.Title = record.Title;
        entity.Department = record.Department;
        entity.Location = record.Location;
        entity.EmploymentType = record.EmploymentType;
        entity.WorkMode = record.WorkMode;
        entity.Experience = record.Experience;
        entity.Summary = record.Summary;
        entity.ResponsibilitiesJson = record.ResponsibilitiesJson;
        entity.RequirementsJson = record.RequirementsJson;
        entity.Status = string.IsNullOrWhiteSpace(record.Status) ? "Draft" : record.Status;
        // IsPublished mirrors Status so the existing career-site queries keep working.
        entity.IsPublished = string.Equals(entity.Status, "Active", StringComparison.OrdinalIgnoreCase);
        entity.ExpiresOn = record.ExpiresOn;
        entity.SalaryMinimum = record.SalaryMinimum;
        entity.SalaryMaximum = record.SalaryMaximum;
        entity.SalaryCurrencyCode = record.SalaryCurrencyCode;
        entity.RequiredDocumentCodesJson = record.RequiredDocumentCodesJson;
        entity.ScreeningQuestionsJson = record.ScreeningQuestionsJson;
        entity.StatusChangedUtc = record.StatusChangedUtc;
        entity.PublishedUtc = record.PublishedUtc;
        entity.UpdatedUtc = record.UpdatedUtc;
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private static JobOpeningRecord ToRecord(JobOpeningEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        Slug = entity.Slug,
        Title = entity.Title,
        Department = entity.Department,
        Location = entity.Location,
        EmploymentType = entity.EmploymentType,
        WorkMode = entity.WorkMode,
        Experience = entity.Experience,
        Summary = entity.Summary,
        ResponsibilitiesJson = entity.ResponsibilitiesJson,
        RequirementsJson = entity.RequirementsJson,
        Status = entity.Status,
        IsPublished = entity.IsPublished,
        ExpiresOn = entity.ExpiresOn,
        SalaryMinimum = entity.SalaryMinimum,
        SalaryMaximum = entity.SalaryMaximum,
        SalaryCurrencyCode = entity.SalaryCurrencyCode,
        RequiredDocumentCodesJson = entity.RequiredDocumentCodesJson,
        ScreeningQuestionsJson = entity.ScreeningQuestionsJson,
        StatusChangedUtc = entity.StatusChangedUtc,
        PublishedUtc = entity.PublishedUtc,
        UpdatedUtc = entity.UpdatedUtc
    };
}
