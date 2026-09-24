using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Admin.Controllers;

[ApiController]
[Authorize(Policy = "AdminPortal")]
public abstract class AdminMobileApiControllerBase : ControllerBase
{
    protected ObjectResult FeatureNotReady(string area, params string[] requiredComponents)
    {
        var problem = new ProblemDetails
        {
            Type = "https://api.hr.example.com/problems/feature-not-ready",
            Title = $"The {area} Admin API is not ready.",
            Detail = "The protected management route is registered, but SQL persistence and business services are not implemented yet.",
            Status = StatusCodes.Status501NotImplemented,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = "FEATURE_NOT_READY";
        problem.Extensions["requiredComponents"] = requiredComponents;
        problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
        return new ObjectResult(problem) { StatusCode = StatusCodes.Status501NotImplemented };
    }
}

[Route("api/admin/v1/mobile/configurations")]
public sealed class AdminMobileConfigurationController : AdminMobileApiControllerBase
{
    [HttpGet]
    public IActionResult GetConfiguration() =>
        FeatureNotReady("Mobile Configuration", "platform.MobileAppConfigurations", "configuration service", "publisher and validator");
}

[Route("api/admin/v1/mobile/catalog")]
public sealed class AdminMobileCatalogController : AdminMobileApiControllerBase
{
    [HttpGet]
    public IActionResult GetCatalog() =>
        FeatureNotReady("Mobile Catalog", "route catalog", "widget catalog", "module catalog");
}

[Route("api/admin/v1/mobile/releases")]
public sealed class AdminMobileReleaseController : AdminMobileApiControllerBase
{
    [HttpGet]
    public IActionResult GetReleases() =>
        FeatureNotReady("Mobile Releases", "platform.MobileReleasePolicies", "release policy service");
}

[Route("api/admin/v1/mobile/maintenance")]
public sealed class AdminMobileMaintenanceController : AdminMobileApiControllerBase
{
    [HttpGet]
    public IActionResult GetMaintenance() =>
        FeatureNotReady("Mobile Maintenance", "platform.MobileMaintenanceWindows", "availability service");
}

[Route("api/admin/v1/mobile/integrations")]
public sealed class AdminMobileIntegrationController : AdminMobileApiControllerBase
{
    [HttpGet]
    public IActionResult GetIntegrations() =>
        FeatureNotReady("Mobile Integrations", "integration reference entity", "secret-vault adapter", "health verifier");
}

[Route("api/admin/v1/mobile/devices")]
public sealed class AdminMobileDeviceController : AdminMobileApiControllerBase
{
    [HttpGet]
    public IActionResult GetDevices() =>
        FeatureNotReady("Mobile Devices", "device session entity", "revocation service");
}

[Route("api/admin/v1/mobile/notifications")]
public sealed class AdminMobileNotificationController : AdminMobileApiControllerBase
{
    [HttpGet]
    public IActionResult GetNotifications() =>
        FeatureNotReady("Mobile Notifications", "event/template configuration", "push provider adapter");
}

[Route("api/admin/v1/mobile/audit")]
public sealed class AdminMobileAuditController : AdminMobileApiControllerBase
{
    [HttpGet]
    public IActionResult GetAudit() =>
        FeatureNotReady("Mobile Audit", "configuration versions", "audit repository", "diff service");
}
