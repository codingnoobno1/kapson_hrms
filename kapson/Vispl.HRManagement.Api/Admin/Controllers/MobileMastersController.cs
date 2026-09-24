using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Mobile.Configuration;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Admin.Controllers;

[Route("api/mobile/v1/masters")]
public sealed class MobileMastersController(IMasterDataService masterDataService)
    : MobileApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<ApiEnvelope<MasterDataBundleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public async Task<ActionResult<ApiEnvelope<MasterDataBundleDto>>> GetAsync(
        [FromQuery] string? category,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<MasterDataBundleDto>();
        }

        var values = string.IsNullOrWhiteSpace(category)
            ? await masterDataService.GetAllAsync(TenantKey, cancellationToken)
            : await masterDataService.GetActiveAsync(TenantKey, category, cancellationToken);

        var activeValues = values
            .Where(item => item.IsActive)
            .OrderBy(item => item.Category)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.Name)
            .ToList();

        var version = activeValues.Count == 0
            ? 0
            : activeValues.Max(item => item.UpdatedUtc.UtcTicks);
        var etag = $"\"masters-{version}\"";

        if (Request.Headers.IfNoneMatch.Any(item => string.Equals(item, etag, StringComparison.Ordinal)))
        {
            return StatusCode(StatusCodes.Status304NotModified);
        }

        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "private, max-age=300";

        var categories = activeValues
            .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<MasterOptionMobileDto>)group
                    .Select(item => new MasterOptionMobileDto(
                        item.Id,
                        item.Category,
                        item.Code,
                        item.Name,
                        item.SortOrder))
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        return OkEnvelope(new MasterDataBundleDto(version, DateTimeOffset.UtcNow, categories));
    }
}
