using Vispl.HRManagement.Contracts.Mobile.Bootstrap;

namespace Vispl.HRManagement.MobileApp.Services.Clients;

public sealed class DiscoveryApi(HrApiClient client)
{
    public Task<ApiResult<PublicMobileMetadataDto>> GetMetadataAsync(CancellationToken cancellationToken = default) =>
        client.GetAsync<PublicMobileMetadataDto>(ApiCatalog.MetadataPath, cancellationToken: cancellationToken);

    public Task<ApiResult<TenantDiscoveryDto>> ResolveTenantAsync(
        string companyCodeOrDomain,
        CancellationToken cancellationToken = default) =>
        client.PostAsync<TenantDiscoveryDto>(
            ApiCatalog.ResolveTenantPath,
            new ResolveTenantRequest { CompanyCodeOrDomain = companyCodeOrDomain },
            cancellationToken);
}
