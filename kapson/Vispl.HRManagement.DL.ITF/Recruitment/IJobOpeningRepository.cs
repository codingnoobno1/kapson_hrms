using Vispl.HRManagement.VO.Recruitment;

namespace Vispl.HRManagement.DL.ITF.Recruitment;

public interface IJobOpeningRepository
{
    Task<IReadOnlyList<JobOpeningRecord>> GetPublishedAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobOpeningRecord>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<JobOpeningRecord?> GetBySlugAsync(string tenantKey, string slug, bool publishedOnly, CancellationToken cancellationToken = default);
    Task<JobOpeningRecord?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<long> UpsertAsync(JobOpeningRecord record, CancellationToken cancellationToken = default);
}
