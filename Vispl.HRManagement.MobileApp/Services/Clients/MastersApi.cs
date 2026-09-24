using Vispl.HRManagement.Contracts.Mobile.Configuration;

namespace Vispl.HRManagement.MobileApp.Services.Clients;

public sealed class MastersApi(HrApiClient client)
{
    private MasterDataBundleDto? cached;
    private string? cachedETag;

    public async Task<ApiResult<MasterDataBundleDto>> GetAsync(
        string? category = null,
        CancellationToken cancellationToken = default)
    {
        var path = string.IsNullOrWhiteSpace(category)
            ? ApiCatalog.MastersPath
            : $"{ApiCatalog.MastersPath}?category={Uri.EscapeDataString(category)}";

        var useCache = string.IsNullOrWhiteSpace(category) && cached is not null;
        var result = await client.GetAsync<MasterDataBundleDto>(
            path,
            useCache ? cachedETag : null,
            cancellationToken);

        if (result.Outcome == ApiOutcome.NotModified && cached is not null)
        {
            return ApiResult<MasterDataBundleDto>.Success(cached, cached.Version, cachedETag);
        }

        if (result.IsSuccess && string.IsNullOrWhiteSpace(category))
        {
            cached = result.Data;
            cachedETag = result.ETag;
        }

        return result;
    }

    public void Invalidate()
    {
        cached = null;
        cachedETag = null;
    }
}
