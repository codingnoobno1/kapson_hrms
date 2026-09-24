using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vispl.HRManagement.Worker.Shared;

public static class HrWorkerExtensions
{
    public static IServiceCollection AddHrWorkers(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register module-owned hosted services here as they are implemented.
        services.AddHostedService<Workforce.AttendanceDayCloseWorker>();
        services.AddHostedService<Workforce.OverdueMovementWorker>();

        return services;
    }
}
