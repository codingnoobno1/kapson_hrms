using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.Identity.Recruitment;

namespace Vispl.HRManagement.Composition.Recruitment;

public static class RecruitmentModule
{
    public static IServiceCollection AddRecruitmentModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRecruitmentIdentity(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapRecruitment(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/recruitment")
            .MapGet("/health", () => Results.Ok(new { module = "Recruitment", status = "Healthy" }))
            .AllowAnonymous();
        return endpoints;
    }
}
