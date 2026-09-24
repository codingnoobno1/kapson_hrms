using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.Contracts.Mobile.Bootstrap;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Shared.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/mobile/v1/discovery")]
public sealed class MobileDiscoveryController(
    ITenantService tenantService,
    ICareerSiteService careerSiteService,
    IOptions<MobileApiOptions> options) : ControllerBase
{
    [HttpGet("metadata")]
    [ProducesResponseType<ApiEnvelope<PublicMobileMetadataDto>>(StatusCodes.Status200OK)]
    public ActionResult<ApiEnvelope<PublicMobileMetadataDto>> GetMetadata()
    {
        var value = options.Value;
        return OkEnvelope(new PublicMobileMetadataDto(
            value.ApiVersion,
            value.MinimumAppVersion,
            value.SupportedPlatforms,
            value.Authority,
            value.Audience,
            value.StatusUrl,
            value.LegalLinks));
    }

    [HttpPost("resolve-tenant")]
    [ProducesResponseType<ApiEnvelope<TenantDiscoveryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiEnvelope<TenantDiscoveryDto>>> ResolveTenantAsync(
        [FromBody] ResolveTenantRequest request,
        CancellationToken cancellationToken)
    {
        var lookup = NormalizeLookup(request.CompanyCodeOrDomain);
        var tenant = await tenantService.GetByKeyAsync(lookup, cancellationToken);

        if (tenant is null)
        {
            var tenants = await tenantService.GetAllAsync(cancellationToken);
            tenant = tenants.FirstOrDefault(item =>
                string.Equals(NormalizeLookup(item.PrimaryDomain), lookup, StringComparison.OrdinalIgnoreCase));
        }

        if (tenant is null || !tenant.IsActive)
        {
            return NotFound(new ProblemDetails
            {
                Type = "https://api.hr.example.com/problems/tenant-not-found",
                Title = "The company could not be found or is unavailable.",
                Status = StatusCodes.Status404NotFound,
                Instance = HttpContext.Request.Path,
                Extensions =
                {
                    ["code"] = "TENANT_NOT_FOUND",
                    ["correlationId"] = HttpContext.TraceIdentifier
                }
            });
        }

        var site = await careerSiteService.GetPublishedAsync(tenant.TenantKey, cancellationToken);
        var result = new TenantDiscoveryDto(
            tenant.TenantKey,
            tenant.TenantKey,
            tenant.DisplayName,
            site?.LogoUrl,
            options.Value.Authority,
            string.IsNullOrWhiteSpace(options.Value.Authority) ? [] : ["oidc"],
            true);

        return OkEnvelope(result);
    }

    private ActionResult<ApiEnvelope<T>> OkEnvelope<T>(T data) =>
        Ok(new ApiEnvelope<T>(
            data,
            new ApiMetaDto(HttpContext.TraceIdentifier, DateTimeOffset.UtcNow, 0, [])));

    private static string NormalizeLookup(string? value)
    {
        var normalized = value?.Trim().ToLowerInvariant() ?? string.Empty;
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            return uri.Host.ToLowerInvariant();
        }

        var slashIndex = normalized.IndexOf('/');
        return slashIndex >= 0 ? normalized[..slashIndex] : normalized;
    }
}
