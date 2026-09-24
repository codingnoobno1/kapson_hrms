using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.Identity.Employee;

namespace Vispl.HRManagement.Composition.Employee;

public static class EmployeeModule
{
    public static IServiceCollection AddEmployeeModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddEmployeeIdentity(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapEmployee(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGroup("/employee")
            .MapGet("/health", () => Results.Ok(new { module = "Employee", status = "Healthy" }))
            .AllowAnonymous();
        return endpoints;
    }
}
