namespace Vispl.HRManagement.VO.Recruitment;

public sealed record CandidateRecord(
    long Id,
    string TenantKey,
    string Email,
    string FullName,
    string Phone,
    string? CurrentLocation,
    decimal TotalExperienceYears,
    string? AccountId,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record ApplicationRecord(
    long Id,
    string TenantKey,
    long JobOpeningId,
    long CandidateId,
    string Stage,
    string Source,
    int NoticePeriodDays,
    decimal? ExpectedCtc,
    string AnswersJson,
    decimal? Rating,
    string? RejectionReason,
    bool ConsentGranted,
    string? ConsentPolicyVersion,
    DateTimeOffset? ConsentUtc,
    DateTimeOffset AppliedUtc,
    DateTimeOffset StageChangedUtc,
    string? StageChangedBy,
    DateTimeOffset UpdatedUtc);

public sealed record ApplicationDocumentRecord(
    long Id,
    string TenantKey,
    long ApplicationId,
    string CategoryCode,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string StoragePath,
    string ScanState,
    DateTimeOffset UploadedUtc);

public sealed record ApplicationEventRecord(
    long Id,
    string TenantKey,
    long ApplicationId,
    string FromStage,
    string ToStage,
    string? Reason,
    string? ActorId,
    string ActorKind,
    DateTimeOffset OccurredUtc);

/// <summary>An application joined to the candidate and opening rows the list view needs.</summary>
public sealed record ApplicationSummaryRecord(
    ApplicationRecord Application,
    CandidateRecord Candidate,
    string JobTitle,
    string Department);

public sealed record OnboardingRecord(
    long Id,
    string TenantKey,
    long ApplicationId,
    long CandidateId,
    string Stage,
    DateOnly? JoiningOn,
    DateTimeOffset StartedUtc,
    DateTimeOffset? CompletedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record OnboardingTaskRecord(
    long Id,
    string TenantKey,
    long OnboardingId,
    string Code,
    string Title,
    string Instructions,
    string TaskType,
    string OwnerKind,
    bool IsRequired,
    int SortOrder,
    string Status,
    long? DocumentId,
    string? FormValuesJson,
    string? DecisionReason,
    string? DecidedBy,
    DateTimeOffset? DecidedUtc,
    DateTimeOffset UpdatedUtc);
