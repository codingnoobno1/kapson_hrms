using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

public interface ICareerSiteService
{
    Task<CareerSiteConfigurationDto?> GetDraftAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<CareerSiteConfigurationDto?> GetPublishedAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task SaveDraftAsync(CareerSiteConfigurationDto configuration, CancellationToken cancellationToken = default);
    Task PublishAsync(string tenantKey, CancellationToken cancellationToken = default);
}
