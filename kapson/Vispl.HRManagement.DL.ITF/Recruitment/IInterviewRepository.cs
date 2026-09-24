using Vispl.HRManagement.VO.Recruitment;

namespace Vispl.HRManagement.DL.ITF.Recruitment;

public sealed record InterviewFilter(
    DateOnly? From,
    DateOnly? To,
    string? Status,
    string? PanelMemberId,
    long? ApplicationId);

public interface IInterviewRepository
{
    /// <summary>Interviews joined to the candidate and opening the schedule screen displays.</summary>
    Task<IReadOnlyList<InterviewSummaryRecord>> QueryAsync(
        string tenantKey, InterviewFilter filter, CancellationToken cancellationToken = default);

    Task<InterviewSummaryRecord?> GetAsync(
        string tenantKey, long interviewId, CancellationToken cancellationToken = default);

    Task<long> UpsertAsync(InterviewRecord interview, CancellationToken cancellationToken = default);

    /// <summary>
    /// Interviews for these panel members that overlap the window, used to find a slot where
    /// nobody is already booked.
    /// </summary>
    Task<IReadOnlyList<InterviewRecord>> GetPanelBookingsAsync(
        string tenantKey,
        IReadOnlyList<string> panelMemberIds,
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InterviewPanelistRecord>> GetPanelAsync(
        string tenantKey, long interviewId, CancellationToken cancellationToken = default);

    Task ReplacePanelAsync(
        string tenantKey, long interviewId, IReadOnlyList<InterviewPanelistRecord> panel,
        CancellationToken cancellationToken = default);

    Task<InterviewEvaluationRecord?> GetEvaluationAsync(
        string tenantKey, long interviewId, string panelMemberId, CancellationToken cancellationToken = default);

    Task<long> UpsertEvaluationAsync(
        InterviewEvaluationRecord evaluation, CancellationToken cancellationToken = default);
}
