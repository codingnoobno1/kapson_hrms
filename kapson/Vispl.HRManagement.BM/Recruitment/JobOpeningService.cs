using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.Contracts.Recruitment;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.VO.Recruitment;

namespace Vispl.HRManagement.BM.Recruitment;

public sealed class JobOpeningService(IJobOpeningRepository repository) : IJobOpeningService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<JobOpeningDto>> GetPublishedAsync(
        string tenantKey,
        CancellationToken cancellationToken = default) =>
        (await repository.GetPublishedAsync(Normalize(tenantKey), cancellationToken)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<JobOpeningDto>> GetAllAsync(
        string tenantKey,
        CancellationToken cancellationToken = default) =>
        (await repository.GetAllAsync(Normalize(tenantKey), cancellationToken)).Select(ToDto).ToList();

    public async Task<JobOpeningDto?> GetPublishedBySlugAsync(
        string tenantKey,
        string slug,
        CancellationToken cancellationToken = default)
    {
        var record = await repository.GetBySlugAsync(Normalize(tenantKey), Normalize(slug), true, cancellationToken);
        return record is null ? null : ToDto(record);
    }

    public async Task<JobOpeningDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        var record = await repository.GetByIdAsync(id, cancellationToken);
        return record is null ? null : ToDto(record);
    }

    public async Task<long> SaveAsync(JobOpeningDto opening, CancellationToken cancellationToken = default)
    {
        opening.TenantKey = Normalize(opening.TenantKey);
        opening.Slug = Normalize(opening.Slug);
        var existing = opening.Id == 0 ? null : await repository.GetByIdAsync(opening.Id, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        return await repository.UpsertAsync(new JobOpeningRecord
        {
            Id = opening.Id,
            TenantKey = opening.TenantKey,
            Slug = opening.Slug,
            Title = opening.Title.Trim(),
            Department = opening.Department.Trim(),
            Location = opening.Location.Trim(),
            EmploymentType = opening.EmploymentType.Trim(),
            WorkMode = opening.WorkMode.Trim(),
            Experience = opening.Experience.Trim(),
            Summary = opening.Summary.Trim(),
            ResponsibilitiesJson = JsonSerializer.Serialize(opening.Responsibilities.Where(item => !string.IsNullOrWhiteSpace(item)), JsonOptions),
            RequirementsJson = JsonSerializer.Serialize(opening.Requirements.Where(item => !string.IsNullOrWhiteSpace(item)), JsonOptions),
            Status = string.IsNullOrWhiteSpace(opening.Status) ? "Draft" : opening.Status,
            IsPublished = opening.IsPublished,
            ExpiresOn = opening.ExpiresOn,
            SalaryMinimum = opening.SalaryMinimum,
            SalaryMaximum = opening.SalaryMaximum,
            SalaryCurrencyCode = opening.SalaryCurrencyCode,
            StatusChangedUtc = opening.Status == existing?.Status ? existing?.StatusChangedUtc : now,
            PublishedUtc = opening.IsPublished ? existing?.PublishedUtc ?? now : null,
            UpdatedUtc = now
        }, cancellationToken);
    }

    public async Task SetPublishedAsync(
        long id,
        bool isPublished,
        CancellationToken cancellationToken = default)
    {
        var existing = await repository.GetByIdAsync(id, cancellationToken)
            ?? throw new KeyNotFoundException($"Job opening {id} was not found.");
        existing.IsPublished = isPublished;
        existing.PublishedUtc = isPublished ? existing.PublishedUtc ?? DateTimeOffset.UtcNow : null;
        existing.UpdatedUtc = DateTimeOffset.UtcNow;
        await repository.UpsertAsync(existing, cancellationToken);
    }

    private static JobOpeningDto ToDto(JobOpeningRecord record) => new()
    {
        Id = record.Id,
        TenantKey = record.TenantKey,
        Slug = record.Slug,
        Title = record.Title,
        Department = record.Department,
        Location = record.Location,
        EmploymentType = record.EmploymentType,
        WorkMode = record.WorkMode,
        Experience = record.Experience,
        Summary = record.Summary,
        Responsibilities = JsonSerializer.Deserialize<List<string>>(record.ResponsibilitiesJson, JsonOptions) ?? [],
        Requirements = JsonSerializer.Deserialize<List<string>>(record.RequirementsJson, JsonOptions) ?? [],
        Status = EffectiveStatus(record),
        IsPublished = record.IsPublished,
        ExpiresOn = record.ExpiresOn,
        SalaryMinimum = record.SalaryMinimum,
        SalaryMaximum = record.SalaryMaximum,
        SalaryCurrencyCode = record.SalaryCurrencyCode,
        StatusChangedUtc = record.StatusChangedUtc,
        PublishedUtc = record.PublishedUtc,
        UpdatedUtc = record.UpdatedUtc
    };

    /// <summary>
    /// Expiry is a date, not a stored state, so an opening becomes Expired by the calendar
    /// rather than by someone remembering to change it.
    /// </summary>
    private static string EffectiveStatus(JobOpeningRecord record)
    {
        if (record.ExpiresOn is { } expires && expires < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return "Expired";
        }

        return string.IsNullOrWhiteSpace(record.Status)
            ? (record.IsPublished ? "Active" : "Draft")
            : record.Status;
    }

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToLowerInvariant();
    }
}
