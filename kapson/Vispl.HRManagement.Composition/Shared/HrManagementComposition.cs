using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.Composition.Admin;
using Vispl.HRManagement.Composition.Candidate;
using Vispl.HRManagement.Composition.Employee;
using Vispl.HRManagement.Composition.Recruitment;
using Vispl.HRManagement.BM.Shared;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.Identity.Shared;
using Vispl.HRManagement.Identity.Admin;
using Vispl.HRManagement.Worker.Shared;
using Vispl.HRManagement.Mail.Shared;

namespace Vispl.HRManagement.Composition.Shared;

public static class HrManagementComposition
{
    public static IServiceCollection AddHrManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHrManagementData(configuration);
        services.AddHrManagementBusiness();
        services.AddSingleton<BM.ITF.Shared.ISensitiveValueProtector, DataProtectionSensitiveValueProtector>();
        services.AddHrIdentity(configuration);
        services.AddAdminModule(configuration);
        services.AddRecruitmentModule(configuration);
        services.AddCandidateModule(configuration);
        services.AddEmployeeModule(configuration);
        services.AddHrMail(configuration);
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

    public static async Task InitializeHrManagementAsync(
        this WebApplication application,
        CancellationToken cancellationToken = default)
    {
        if (!application.Configuration.GetValue("Database:InitializeOnStartup", true))
        {
            return;
        }

        var configuredPath = application.Configuration["Database:SeedDataPath"] ?? "SeedData";
        var seedDataPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(application.Environment.ContentRootPath, configuredPath);

        await using var scope = application.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<HrManagementDatabaseInitializer>();
        await initializer.InitializeAsync(
            seedDataPath,
            application.Configuration.GetValue("Database:SeedOnEmpty", true),
            cancellationToken);

        var identityInitializer = scope.ServiceProvider.GetRequiredService<AdminIdentityInitializer>();
        await identityInitializer.InitializeAsync(cancellationToken);

        // Opt-in, Development-only, and a no-op unless DevHrAccount credentials are configured.
        var developmentHrSeeder = scope.ServiceProvider.GetRequiredService<DevelopmentHrAccountSeeder>();
        await developmentHrSeeder.SeedAsync(cancellationToken);
    }
}
