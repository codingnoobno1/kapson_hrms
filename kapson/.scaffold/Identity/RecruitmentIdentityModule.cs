using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vispl.HRManagement.Identity.Recruitment;

public static class RecruitmentIdentityModule
{
    public static IServiceCollection AddRecruitmentIdentity(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
