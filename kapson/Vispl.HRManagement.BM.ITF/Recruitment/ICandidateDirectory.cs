namespace Vispl.HRManagement.BM.ITF.Recruitment;

public sealed record CandidateIdentityDto(
    long CandidateId,
    string Email,
    string FullName,
    string? AccountId);

/// <summary>
/// The narrow slice of the candidate record that identity needs. It exists so the identity
/// module can activate an applicant without referencing recruitment persistence directly.
/// </summary>
public interface ICandidateDirectory
{
    Task<CandidateIdentityDto?> FindByEmailAsync(
        string tenantKey, string email, CancellationToken cancellationToken = default);

    Task LinkAccountAsync(
        string tenantKey, long candidateId, string accountId, CancellationToken cancellationToken = default);
}
