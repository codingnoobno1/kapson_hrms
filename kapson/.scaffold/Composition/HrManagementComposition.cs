using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.Composition.Admin;
using Vispl.HRManagement.Composition.Candidate;
using Vispl.HRManagement.Composition.Employee;
using Vispl.HRManagement.Composition.Recruitment;
using Vispl.HRManagement.Identity.Shared;
using Vispl.HRManagement.Worker.Shared;

namespace Vispl.HRManagement.Composition.Shared;

public static class HrManagementComposition
{
    public static IServiceCollection AddHrManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHrIdentity(configuration);
        services.AddAdminModule(configuration);
        services.AddRecruitmentModule(configuration);
        services.AddCandidateModule(configuration);
        services.AddEmployeeModule(configuration);
        services.AddHrWorkers(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapHrManagement(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHrIdentityEndpoints();
        endpoints.MapAdmin();
        endpoints.MapRecruitment();
        endpoints.MapCandidate();
        endpoints.MapEmployee();
        return endpoints;
    }
}
