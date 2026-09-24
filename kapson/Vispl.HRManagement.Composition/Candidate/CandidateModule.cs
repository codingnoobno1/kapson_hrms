using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.Identity.Candidate;

namespace Vispl.HRManagement.Composition.Candidate;

public static class CandidateModule
{
    public static IServiceCollection AddCandidateModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCandidateIdentity(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapCandidate(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/candidate")
            .MapGet("/health", () => Results.Ok(new { module = "Candidate", status = "Healthy" }))
            .AllowAnonymous();
        return endpoints;
    }
}
