using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Recruitment;

public sealed class JobListQuery : MobileListQuery
{
    [StringLength(80)] public string? Department { get; set; }
    [StringLength(180)] public string? Location { get; set; }
    [StringLength(40)] public string? Status { get; set; }
}

public sealed record JobListItemDto(
    long Id,
    string Slug,
    string Title,
    string Department,
    string Location,
    string EmploymentType,
    string WorkMode,
    string Experience,
    string Summary,
    bool IsPublished,
    DateTimeOffset? PublishedUtc,
    DateTimeOffset UpdatedUtc,
    // Fields the reference job card renders. Status is the four-state lifecycle;
    // IsPublished stays for callers that only care whether it is live.
    string Status = "Draft",
    DateOnly? ExpiresOn = null,
    string? SalaryRange = null,
    DateOnly? LastStatusUpdatedOn = null);

public sealed record JobDetailDto(
    long Id,
    string Slug,
    string Title,
    string Department,
    string Location,
    string EmploymentType,
    string WorkMode,
    string Experience,
    string Summary,
    IReadOnlyList<string> Responsibilities,
    IReadOnlyList<string> Requirements,
    bool IsPublished,
    DateTimeOffset? PublishedUtc,
    DateTimeOffset UpdatedUtc,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string Status = "Draft",
    DateOnly? ExpiresOn = null,
    string? SalaryRange = null,
    DateOnly? LastStatusUpdatedOn = null);

public sealed record RecruitmentDashboardDto(
    IReadOnlyList<KpiDto> Kpis,
    IReadOnlyDictionary<string, int> PipelineCounts,
    IReadOnlyList<InterviewListItemDto> UpcomingInterviews);

public sealed record RequisitionListItemDto(
    string Id,
    string Title,
    string Department,
    string Location,
    int Headcount,
    PersonReferenceDto Owner,
    StatusDto Status,
    DateTimeOffset UpdatedUtc,
    string? RowVersion);

public sealed record RequisitionDetailDto(
    RequisitionListItemDto Summary,
    string Justification,
    MoneyDto? BudgetMinimum,
    MoneyDto? BudgetMaximum,
    IReadOnlyList<WorkflowStepDto> ApprovalSteps,
    IReadOnlyList<ActionAvailabilityDto> Actions);

public sealed record ApplicationListItemDto(
    string Id,
    PersonReferenceDto Candidate,
    long JobId,
    string JobTitle,
    StatusDto Stage,
    decimal? Rating,
    DateTimeOffset AppliedUtc,
    IReadOnlyList<ActionAvailabilityDto> Actions);

public sealed record ApplicationDetailDto(
    ApplicationListItemDto Summary,
    string Source,
    IReadOnlyList<AttachmentMetadataDto> Documents,
    IReadOnlyList<AuditEntryDto> Activity,
    string? RowVersion);

public sealed record CandidateProfileDto(
    string Id,
    string DisplayName,
    string? Email,
    string? Phone,
    IReadOnlyList<string> Skills,
    decimal? TotalExperienceYears,
    IReadOnlyList<FieldAccessDto> FieldAccess);

public sealed record InterviewListItemDto(
    string Id,
    PersonReferenceDto Candidate,
    string JobTitle,
    string Round,
    DateTimeOffset StartsUtc,
    int DurationMinutes,
    string Mode,
    StatusDto Status,
    IReadOnlyList<ActionAvailabilityDto> Actions);

public sealed record InterviewDetailDto(
    InterviewListItemDto Summary,
    IReadOnlyList<PersonReferenceDto> Panel,
    string? Location,
    string? JoinUrl,
    string? RowVersion);

public sealed record OfferListItemDto(
    string Id,
    PersonReferenceDto Candidate,
    string JobTitle,
    StatusDto Status,
    DateOnly? ExpiresOn,
    MoneyDto? TotalCompensation,
    IReadOnlyList<ActionAvailabilityDto> Actions);

public sealed record OfferDetailDto(
    OfferListItemDto Summary,
    IReadOnlyList<WorkflowStepDto> ApprovalSteps,
    AttachmentMetadataDto? OfferDocument,
    string? RowVersion);

public sealed class ChangeApplicationStageRequest
{
    [Required, StringLength(80)] public string TargetStage { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(200)] public string RowVersion { get; set; } = string.Empty;
}

public sealed class ApplicationDecisionRequest
{
    [Required, RegularExpression("^(shortlist|reject|return)$")] public string Decision { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string Reason { get; set; } = string.Empty;
    public bool NotifyCandidate { get; set; }
    [Required, StringLength(200)] public string RowVersion { get; set; } = string.Empty;
}
