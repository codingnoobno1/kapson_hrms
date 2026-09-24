using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface ICareerSiteRepository
{
    Task<CareerSiteRecord?> GetAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task UpsertAsync(CareerSiteRecord record, CancellationToken cancellationToken = default);
}
