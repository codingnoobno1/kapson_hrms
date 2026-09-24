using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Vispl.HRManagement.Identity.Candidate;

public static class CandidateIdentityModule
{
    public static IServiceCollection AddCandidateIdentity(
        this IServiceCollection services,
        IConfiguration configuration) => services;
}
