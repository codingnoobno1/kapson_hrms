using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Recruitment;

// Queries -------------------------------------------------------------------

public sealed class RequisitionListQuery : MobileListQuery
{
    [StringLength(40)] public string? Status { get; set; }
    [StringLength(80)] public string? Department { get; set; }
    public bool MineOnly { get; set; }
}

public sealed class ApplicationListQuery : MobileListQuery
{
    public long? JobId { get; set; }
    [StringLength(40)] public string? Stage { get; set; }
    [StringLength(40)] public string? Source { get; set; }
    [Range(0, 5)] public int? MinimumRating { get; set; }
    public int? StalledForDays { get; set; }
}

public sealed class InterviewListQuery : MobileListQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [StringLength(40)] public string? Status { get; set; }
    public bool AssignedToMeOnly { get; set; }
}

public sealed class InterviewAvailabilityQuery
{
    [Required] public string ApplicationId { get; set; } = string.Empty;
    [Required] public DateOnly From { get; set; }
    [Required] public DateOnly To { get; set; }
    [StringLength(64)] public string TimeZoneId { get; set; } = "UTC";

    /// <summary>
    /// A concrete List, not IReadOnlyList: this binds from the query string, and the default
    /// model binder cannot construct a read-only interface. Binding it as an empty collection
    /// would have made every slot look free.
    /// </summary>
    public List<string> PanelMemberIds { get; set; } = [];
    [Range(15, 480)] public int DurationMinutes { get; set; } = 60;
}

public sealed class OfferListQuery : MobileListQuery
{
    [StringLength(40)] public string? Status { get; set; }
    public bool ExpiringOnly { get; set; }
}

public sealed class JobAnalyticsQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

// Requisition ---------------------------------------------------------------

public sealed class SaveRequisitionRequest
{
    [Required, StringLength(180)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(80)] public string DepartmentCode { get; set; } = string.Empty;
    [Required, StringLength(80)] public string LocationCode { get; set; } = string.Empty;
    [Range(1, 999)] public int Headcount { get; set; } = 1;
    [StringLength(40)] public string? EmploymentType { get; set; }
    [StringLength(4000)] public string? Justification { get; set; }
    public decimal? BudgetAmount { get; set; }
    [StringLength(3)] public string? BudgetCurrency { get; set; }
    public DateOnly? RequiredBy { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

// Job -----------------------------------------------------------------------

public sealed class SaveJobRequest
{
    public long? RequisitionId { get; set; }
    [Required, StringLength(180)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Department { get; set; } = string.Empty;
    [Required, StringLength(180)] public string Location { get; set; } = string.Empty;
    [Required, StringLength(40)] public string EmploymentType { get; set; } = string.Empty;
    [Required, StringLength(40)] public string WorkMode { get; set; } = string.Empty;
    [StringLength(80)] public string? Experience { get; set; }
    [Range(1, 999)] public int Openings { get; set; } = 1;
    [StringLength(1000)] public string? Summary { get; set; }
    public IReadOnlyList<string> Responsibilities { get; set; } = [];
    public IReadOnlyList<string> Requirements { get; set; } = [];
    public IReadOnlyList<string> Skills { get; set; } = [];
    public DateOnly? ClosesOn { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class SaveJobCompensationRequest
{
    public decimal? Minimum { get; set; }
    public decimal? Maximum { get; set; }
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Visibility { get; set; } = "Hidden";
    public IReadOnlyList<string> Benefits { get; set; } = [];
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class PublishJobRequest
{
    public DateTimeOffset? ScheduledUtc { get; set; }
    public IReadOnlyList<string> Channels { get; set; } = [];
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class ChangeJobStatusRequest
{
    [Required, StringLength(40)] public string Status { get; set; } = string.Empty;
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed record JobAnalyticsDto(
    long JobId,
    DateRangeDto Range,
    int Views,
    int Applications,
    decimal ConversionRate,
    int AverageAgeDays,
    IReadOnlyDictionary<string, int> BySource,
    IReadOnlyDictionary<string, int> ByStage);

// Application and screening --------------------------------------------------

public sealed record ApplicantPipelineStageDto(
    string StageCode,
    string StageName,
    int Count,
    IReadOnlyList<ApplicationListItemDto> Applications,
    bool HasMore);

public sealed record ApplicantPipelineDto(
    long JobId,
    string JobTitle,
    IReadOnlyList<ApplicantPipelineStageDto> Stages,
    DateTimeOffset GeneratedUtc);

public sealed class RateApplicationRequest
{
    [Range(0, 5)] public decimal Rating { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed record ScreeningCriterionDto(
    string Code,
    string Name,
    decimal? Score,
    decimal MaximumScore,
    decimal Weight,
    string? Notes,
    bool IsRequired);

public sealed record ScreeningScorecardDto(
    string ApplicationId,
    IReadOnlyList<ScreeningCriterionDto> Criteria,
    decimal? Total,
    string? Recommendation,
    string State,
    string? RowVersion);

public sealed class SaveScreeningRequest
{
    public IReadOnlyList<ScreeningCriterionScoreRequest> Scores { get; set; } = [];
    [StringLength(40)] public string? Recommendation { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }
    public bool Submit { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class ScreeningCriterionScoreRequest
{
    [Required, StringLength(40)] public string Code { get; set; } = string.Empty;
    public decimal? Score { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
}

// Candidate documents --------------------------------------------------------

public sealed record CandidateDocumentDto(
    string Id,
    string CandidateId,
    string Category,
    AttachmentMetadataDto File,
    StatusDto VerificationStatus,
    DateOnly? ExpiresOn,
    string? RejectionReason,
    IReadOnlyList<ActionAvailabilityDto> Actions);

public sealed class VerifyDocumentRequest
{
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class RequestDocumentRequest
{
    [Required, StringLength(40)] public string Category { get; set; } = string.Empty;
    [StringLength(1000)] public string? Message { get; set; }
    public DateOnly? DueOn { get; set; }
}

// Interviews -----------------------------------------------------------------

public sealed record InterviewSlotDto(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string TimeZoneId,
    bool AllPanelAvailable,
    IReadOnlyList<string> UnavailablePanelMemberIds);

public sealed record InterviewAvailabilityDto(
    DateRangeDto Range,
    IReadOnlyList<InterviewSlotDto> Slots);

public sealed class ScheduleInterviewRequest
{
    [Required, StringLength(64)] public string ApplicationId { get; set; } = string.Empty;
    [Required, StringLength(40)] public string RoundCode { get; set; } = string.Empty;
    [Required] public DateTimeOffset StartUtc { get; set; }
    [Range(15, 480)] public int DurationMinutes { get; set; } = 60;
    [Required, StringLength(64)] public string TimeZoneId { get; set; } = "UTC";
    [Required, StringLength(20)] public string Mode { get; set; } = "Online";
    [StringLength(400)] public string? LocationOrLink { get; set; }
    public IReadOnlyList<string> PanelMemberIds { get; set; } = [];
    public bool NotifyCandidate { get; set; } = true;
    [StringLength(2000)] public string? Instructions { get; set; }
}

public sealed class RescheduleInterviewRequest
{
    [Required] public DateTimeOffset StartUtc { get; set; }
    [Range(15, 480)] public int DurationMinutes { get; set; } = 60;
    [Required, StringLength(64)] public string TimeZoneId { get; set; } = "UTC";
    [Required, StringLength(40)] public string ReasonCode { get; set; } = string.Empty;
    [StringLength(1000)] public string? Comment { get; set; }
    public bool NotifyCandidate { get; set; } = true;
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed record InterviewEvaluationDto(
    string InterviewId,
    IReadOnlyList<ScreeningCriterionDto> Criteria,
    string? Recommendation,
    string? Notes,
    string State,
    string? RowVersion);

public sealed class SaveInterviewEvaluationRequest
{
    public IReadOnlyList<ScreeningCriterionScoreRequest> Scores { get; set; } = [];
    [StringLength(40)] public string? Recommendation { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }
    public bool Submit { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class CancelInterviewRequest
{
    [Required, StringLength(40)] public string ReasonCode { get; set; } = string.Empty;
    [StringLength(1000)] public string? Comment { get; set; }
    public bool NotifyCandidate { get; set; } = true;
    [StringLength(64)] public string? RowVersion { get; set; }
}

// Offers ----------------------------------------------------------------------

public sealed class GenerateOfferRequest
{
    [Required, StringLength(64)] public string ApplicationId { get; set; } = string.Empty;
    [Required] public DateOnly ProposedJoiningDate { get; set; }
    [Required] public MoneyDto AnnualPackage { get; set; } = new(0m, "INR");
    public IReadOnlyList<OfferComponentRequest> Components { get; set; } = [];
    [StringLength(40)] public string? TemplateCode { get; set; }
    public DateOnly? ValidUntil { get; set; }
    [StringLength(4000)] public string? Notes { get; set; }
}

public sealed class OfferComponentRequest
{
    [Required, StringLength(40)] public string Code { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    [StringLength(20)] public string? Frequency { get; set; }
}

public sealed class OfferDecisionRequest
{
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class SendOfferRequest
{
    [Required, EmailAddress, StringLength(320)] public string ToEmail { get; set; } = string.Empty;
    [StringLength(40)] public string? TemplateCode { get; set; }
    [StringLength(4000)] public string? Message { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}
