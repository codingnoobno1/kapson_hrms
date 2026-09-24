namespace Vispl.HRManagement.BM.ITF.Candidate;

/// <summary>
/// Issues a candidate's activation link and emails it.
///
/// One place, because there are two ways to apply — the public API and the career site's own
/// apply page — and the orchestration used to live in the API controller. Only API
/// submissions got an email; somebody applying through the website was told one had been sent
/// and never received it, which is the worst of both.
///
/// It stays out of <c>IApplicationService</c> deliberately. Recruitment does not know about
/// identity or mail and should not learn; this is the seam where those three meet.
/// </summary>
public interface ICandidateInvitation
{
    /// <summary>
    /// Never throws. Applying has already succeeded by the time this runs, and losing a CV
    /// because a mail server was unreachable is the wrong trade — a missing link can be
    /// requested again from the onboarding page.
    /// </summary>
    Task InviteAsync(
        string tenantKey,
        string email,
        string candidateName,
        CancellationToken cancellationToken = default);
}
