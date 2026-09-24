using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vispl.HRManagement.Identity.Admin;

public static class AdminIdentityModule
{
    public static IServiceCollection AddAdminIdentity(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
