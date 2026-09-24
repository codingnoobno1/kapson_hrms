namespace Vispl.HRManagement.VO.Recruitment;

public sealed record InterviewRecord(
    long Id,
    string TenantKey,
    long ApplicationId,
    long CandidateId,
    long JobOpeningId,
    string RoundCode,
    string RoundLabel,
    DateTimeOffset StartUtc,
    int DurationMinutes,
    string TimeZoneId,
    string Mode,
    string? LocationOrLink,
    string? Instructions,
    string Status,
    string? CancellationReasonCode,
    string? CancellationComment,
    string? ScheduledBy,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc)
{
    public DateTimeOffset EndUtc => StartUtc.AddMinutes(DurationMinutes);

    /// <summary>Two interviews clash when they overlap at all, not merely when they start together.</summary>
    public bool Overlaps(DateTimeOffset otherStart, DateTimeOffset otherEnd) =>
        StartUtc < otherEnd && otherStart < EndUtc;
}

/// <summary>An interview with the candidate and job details the list screen renders.</summary>
public sealed record InterviewSummaryRecord(
    InterviewRecord Interview,
    string CandidateName,
    string? CandidateEmail,
    string JobTitle,
    string Department);

public sealed record InterviewPanelistRecord(
    long Id,
    string TenantKey,
    long InterviewId,
    string PanelMemberId,
    string DisplayName,
    bool IsLead,
    DateTimeOffset AddedUtc);

public sealed record InterviewEvaluationRecord(
    long Id,
    string TenantKey,
    long InterviewId,
    string PanelMemberId,
    string ScoresJson,
    string? Recommendation,
    string? Notes,
    string State,
    DateTimeOffset? SubmittedUtc,
    DateTimeOffset UpdatedUtc);
