using Vispl.HRManagement.Contracts.Recruitment;

namespace Vispl.HRManagement.BM.ITF.Recruitment;

public interface IJobOpeningService
{
    Task<IReadOnlyList<JobOpeningDto>> GetPublishedAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobOpeningDto>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<JobOpeningDto?> GetPublishedBySlugAsync(string tenantKey, string slug, CancellationToken cancellationToken = default);
    Task<JobOpeningDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<long> SaveAsync(JobOpeningDto opening, CancellationToken cancellationToken = default);
    Task SetPublishedAsync(long id, bool isPublished, CancellationToken cancellationToken = default);
}
