using Vispl.HRManagement.VO.Recruitment;

namespace Vispl.HRManagement.DL.ITF.Recruitment;

public interface IApplicationRepository
{
    /// <summary>
    /// Applications per opening per stage. One query behind the "who applied to what" board,
    /// rather than one per vacancy.
    /// </summary>
    Task<IReadOnlyList<(long JobOpeningId, string Stage, int Count, DateTimeOffset LatestUtc)>>
        CountByOpeningAndStageAsync(string tenantKey, CancellationToken cancellationToken = default);

    // Candidates
    Task<CandidateRecord?> GetCandidateByEmailAsync(string tenantKey, string email, CancellationToken cancellationToken = default);
    Task<CandidateRecord?> GetCandidateByIdAsync(string tenantKey, long candidateId, CancellationToken cancellationToken = default);
    Task<long> UpsertCandidateAsync(CandidateRecord candidate, CancellationToken cancellationToken = default);

    // Applications
    Task<ApplicationRecord?> GetAsync(string tenantKey, long applicationId, CancellationToken cancellationToken = default);
    Task<ApplicationRecord?> GetByOpeningAndCandidateAsync(string tenantKey, long jobOpeningId, long candidateId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationSummaryRecord>> GetSummariesAsync(string tenantKey, string? stage, string? query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationSummaryRecord>> GetForCandidateAsync(string tenantKey, long candidateId, CancellationToken cancellationToken = default);
    Task<long> UpsertApplicationAsync(ApplicationRecord application, CancellationToken cancellationToken = default);

    // Documents
    Task<IReadOnlyList<ApplicationDocumentRecord>> GetDocumentsAsync(string tenantKey, long applicationId, CancellationToken cancellationToken = default);
    Task<ApplicationDocumentRecord?> GetDocumentAsync(string tenantKey, long documentId, CancellationToken cancellationToken = default);
    Task<long> AddDocumentAsync(ApplicationDocumentRecord document, CancellationToken cancellationToken = default);
    /// <summary>Deletes the rows and returns their storage paths so the caller can remove the blobs.</summary>
    Task<IReadOnlyList<string>> RemoveDocumentsAsync(string tenantKey, long applicationId, IReadOnlyList<string> categoryCodes, CancellationToken cancellationToken = default);

    // Timeline
    Task<IReadOnlyList<ApplicationEventRecord>> GetEventsAsync(string tenantKey, long applicationId, CancellationToken cancellationToken = default);
    Task AddEventAsync(ApplicationEventRecord entry, CancellationToken cancellationToken = default);

    // Onboarding
    Task<OnboardingRecord?> GetOnboardingAsync(string tenantKey, long onboardingId, CancellationToken cancellationToken = default);
    Task<OnboardingRecord?> GetOnboardingByApplicationAsync(string tenantKey, long applicationId, CancellationToken cancellationToken = default);
    Task<long> UpsertOnboardingAsync(OnboardingRecord onboarding, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OnboardingTaskRecord>> GetOnboardingTasksAsync(string tenantKey, long onboardingId, CancellationToken cancellationToken = default);
    Task<OnboardingTaskRecord?> GetOnboardingTaskAsync(string tenantKey, long taskId, CancellationToken cancellationToken = default);
    Task AddOnboardingTasksAsync(IReadOnlyList<OnboardingTaskRecord> tasks, CancellationToken cancellationToken = default);
    Task UpdateOnboardingTaskAsync(OnboardingTaskRecord task, CancellationToken cancellationToken = default);
}
