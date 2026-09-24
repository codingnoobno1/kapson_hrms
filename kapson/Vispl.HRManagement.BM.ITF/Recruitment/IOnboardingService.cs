using Vispl.HRManagement.Contracts.Recruitment;

namespace Vispl.HRManagement.BM.ITF.Recruitment;

public interface IOnboardingService
{
    Task<OnboardingDto?> GetAsync(string tenantKey, long onboardingId, CancellationToken cancellationToken = default);

    Task<OnboardingDto?> GetByApplicationAsync(string tenantKey, long applicationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everything one candidate can see about themselves: each application with its stage, and
    /// the checklist once there is one.
    ///
    /// Exists because a candidate has no way to know their own onboarding id. Without it the
    /// only candidate-facing read is "fetch onboarding 15", which nobody arriving from an email
    /// could possibly supply.
    /// </summary>
    Task<CandidateProgressDto> GetCandidateProgressAsync(
        string tenantKey, long candidateId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Candidate accepts the offer. Moves the application to Onboarding and seeds the checklist.
    /// The candidate id comes from the caller's token, never from the request body.
    /// </summary>
    Task<long> AcceptOfferAsync(
        string tenantKey, long applicationId, long candidateId, DateOnly? joiningOn, CancellationToken cancellationToken = default);

    Task DeclineOfferAsync(
        string tenantKey, long applicationId, long candidateId, string? reason, CancellationToken cancellationToken = default);

    Task SubmitDocumentTaskAsync(
        string tenantKey, long onboardingId, long taskId, long candidateId, UploadedFile file, CancellationToken cancellationToken = default);

    Task SubmitFormTaskAsync(
        string tenantKey, long onboardingId, long taskId, long candidateId, IReadOnlyDictionary<string, string> values, CancellationToken cancellationToken = default);

    /// <summary>
    /// HR records the outcome of a task they own. There is no separate approval step: for an
    /// HR-owned task the person completing it is the approver, so a second click would be theatre.
    /// </summary>
    Task CompleteHrTaskAsync(
        string tenantKey, long onboardingId, long taskId,
        IReadOnlyDictionary<string, string> values, string? actorId, CancellationToken cancellationToken = default);

    /// <summary>HR approves or rejects a submitted task. Completing the last required task finishes onboarding.</summary>
    Task DecideTaskAsync(
        string tenantKey, long taskId, OnboardingTaskDecisionRequest request, string? actorId, CancellationToken cancellationToken = default);
}
