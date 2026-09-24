using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vispl.HRManagement.Identity.Shared;

public static class HrIdentityExtensions
{
    public static IServiceCollection AddHrIdentity(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAuthentication();
        services.AddAuthorization();
        return services;
    }

    public static IEndpointRouteBuilder MapHrIdentityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/identity/health", () =>
            Results.Ok(new { module = "Identity", status = "Healthy" }))
            .AllowAnonymous();

        return endpoints;
    }
}
