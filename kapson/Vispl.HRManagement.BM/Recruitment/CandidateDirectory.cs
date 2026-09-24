using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.DL.ITF.Recruitment;

namespace Vispl.HRManagement.BM.Recruitment;

public sealed class CandidateDirectory(IApplicationRepository applications) : ICandidateDirectory
{
    public async Task<CandidateIdentityDto?> FindByEmailAsync(
        string tenantKey, string email, CancellationToken cancellationToken = default)
    {
        var candidate = await applications.GetCandidateByEmailAsync(tenantKey, email, cancellationToken);
        return candidate is null
            ? null
            : new CandidateIdentityDto(candidate.Id, candidate.Email, candidate.FullName, candidate.AccountId);
    }

    public async Task LinkAccountAsync(
        string tenantKey, long candidateId, string accountId, CancellationToken cancellationToken = default)
    {
        var candidate = await applications.GetCandidateByIdAsync(tenantKey, candidateId, cancellationToken);
        if (candidate is null)
        {
            return;
        }

        await applications.UpsertCandidateAsync(
            candidate with { AccountId = accountId, UpdatedUtc = DateTimeOffset.UtcNow },
            cancellationToken);
    }
}
