using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vispl.HRManagement.Identity.Employee;

public static class EmployeeIdentityModule
{
    public static IServiceCollection AddEmployeeIdentity(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
