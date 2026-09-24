using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Employee;

// Queries -------------------------------------------------------------------

public sealed class EmployeeListQuery : MobileListQuery
{
    [StringLength(80)] public string? Department { get; set; }
    [StringLength(80)] public string? Location { get; set; }
    [StringLength(40)] public string? Status { get; set; }
    [StringLength(64)] public string? ManagerId { get; set; }
}

public sealed class EmployeeTimelineQuery : MobileListQuery
{
    [StringLength(40)] public string? EventType { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed class OnboardingQuery : MobileListQuery
{
    [StringLength(40)] public string? CohortCode { get; set; }
    [StringLength(40)] public string? State { get; set; }
    public bool OverdueOnly { get; set; }
}

// Employee record updates ----------------------------------------------------

public sealed class UpdateEmployeePersonalRequest
{
    [Required, StringLength(120)] public string LegalFirstName { get; set; } = string.Empty;
    [StringLength(120)] public string? LegalMiddleName { get; set; }
    [Required, StringLength(120)] public string LegalLastName { get; set; } = string.Empty;
    [StringLength(120)] public string? PreferredName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    [StringLength(40)] public string? Gender { get; set; }
    [StringLength(40)] public string? MaritalStatus { get; set; }
    [StringLength(80)] public string? Nationality { get; set; }
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class UpdateEmployeeEmploymentRequest
{
    public DateOnly? JoinedOn { get; set; }
    [StringLength(40)] public string? EmploymentType { get; set; }
    [StringLength(40)] public string? EmploymentStatus { get; set; }
    [StringLength(40)] public string? Grade { get; set; }
    public DateOnly? ProbationEndsOn { get; set; }

    /// <summary>Report Configuration's "Date of Leaving". Set on exit; leave null otherwise.</summary>
    public DateOnly? LeavingOn { get; set; }

    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class UpdateEmployeeOrganisationRequest
{
    [StringLength(64)] public string? CompanyCode { get; set; }
    [StringLength(64)] public string? OrganisationUnitCode { get; set; }
    [StringLength(64)] public string? DepartmentCode { get; set; }
    [StringLength(64)] public string? LocationCode { get; set; }
    [StringLength(64)] public string? ManagerEmployeeId { get; set; }
    public DateOnly? EffectiveFrom { get; set; }
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class SaveEmployeeContactRequest
{
    [StringLength(64)] public string? Id { get; set; }
    [Required, StringLength(20)] public string Kind { get; set; } = "Emergency";
    [Required, StringLength(160)] public string Name { get; set; } = string.Empty;
    [StringLength(60)] public string? Relationship { get; set; }
    [StringLength(32)] public string? Phone { get; set; }
    [EmailAddress, StringLength(320)] public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool IsPrimary { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

// Field-change request workflow (WF-009) --------------------------------------

public sealed record FieldChangeDto(string Field, string? CurrentValue, string? RequestedValue);

public sealed record EmployeeChangeRequestDto(
    string Id,
    string EmployeeId,
    string Section,
    IReadOnlyList<FieldChangeDto> Changes,
    StatusDto Status,
    IReadOnlyList<AttachmentMetadataDto> Evidence,
    IReadOnlyList<WorkflowStepDto> Workflow,
    DateTimeOffset SubmittedUtc,
    string? RowVersion);

public sealed class CreateEmployeeChangeRequest
{
    [Required, StringLength(40)] public string Section { get; set; } = string.Empty;
    public IReadOnlyList<FieldChangeDto> Changes { get; set; } = [];
    public IReadOnlyList<string> EvidenceUploadIds { get; set; } = [];
    [StringLength(1000)] public string? Reason { get; set; }
}

/// <summary>The two sections the app actually raises change requests for today.</summary>
public static class EmployeeChangeRequestSections
{
    public const string Personal = "Personal";
    public const string BankAccount = "BankAccount";
}

/// <summary>One row of the HR decision queue — enough to show and decide without a second fetch.</summary>
public sealed record EmployeeChangeRequestListItemDto(
    string Id,
    string EmployeeId,
    string EmployeeDisplayName,
    string? EmployeeNumber,
    string? DepartmentCode,
    string Section,
    IReadOnlyList<FieldChangeDto> Changes,
    string? Reason,
    StatusDto Status,
    DateTimeOffset SubmittedUtc);

public sealed class EmployeeChangeRequestQuery
{
    /// <summary>Blank returns every request, newest first; otherwise filters to one status.</summary>
    [StringLength(20)] public string? Status { get; set; } = "Pending";
}

public sealed class DecideEmployeeChangeRequest
{
    [Required, RegularExpression("^(approve|reject)$", ErrorMessage = "Use approve or reject.")]
    public string Decision { get; set; } = string.Empty;

    [StringLength(500)] public string? Comment { get; set; }
}

// Documents -------------------------------------------------------------------

public sealed class DocumentUploadIntentRequest
{
    [Required, StringLength(40)] public string Category { get; set; } = string.Empty;
    [Required, StringLength(260)] public string FileName { get; set; } = string.Empty;
    [Required, StringLength(120)] public string ContentType { get; set; } = string.Empty;
    [Range(1, long.MaxValue)] public long SizeBytes { get; set; }
    public DateOnly? ExpiresOn { get; set; }
}

public sealed class ConfirmDocumentUploadRequest
{
    [Required, StringLength(120)] public string UploadId { get; set; } = string.Empty;
    [StringLength(40)] public string? Category { get; set; }
    public DateOnly? ExpiresOn { get; set; }
}

public sealed class VerifyEmployeeDocumentRequest
{
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

// Onboarding ------------------------------------------------------------------

public sealed record OnboardingCohortDto(
    string CohortCode,
    string Name,
    DateOnly StartsOn,
    int Joiners,
    int Completed,
    int Overdue);

public sealed record OnboardingDashboardDto(
    IReadOnlyList<OnboardingCohortDto> Cohorts,
    IReadOnlyList<OnboardingSummaryDto> Joiners,
    DateTimeOffset GeneratedUtc);

public sealed class CompleteOnboardingTaskRequest
{
    public IReadOnlyList<string> EvidenceUploadIds { get; set; } = [];
    [StringLength(2000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class ReassignOnboardingTaskRequest
{
    [Required, StringLength(64)] public string OwnerId { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
    public DateOnly? DueOn { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class AddTaskCommentRequest
{
    [Required, StringLength(4000)] public string Comment { get; set; } = string.Empty;
}

// Probation --------------------------------------------------------------------

public sealed class SaveProbationReviewRequest
{
    public IReadOnlyList<ProbationGoalRequest> Goals { get; set; } = [];
    [StringLength(4000)] public string? Feedback { get; set; }
    [StringLength(40)] public string? Recommendation { get; set; }
    public bool Submit { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class ProbationGoalRequest
{
    [Required, StringLength(64)] public string Code { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    [Range(0, 5)] public decimal? Rating { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
}

public sealed class ProbationDecisionRequest
{
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    public DateOnly? EffectiveOn { get; set; }
    public DateOnly? ExtendedUntil { get; set; }
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

// Offboarding -------------------------------------------------------------------

public sealed class InitiateOffboardingRequest
{
    [Required, StringLength(40)] public string ReasonCode { get; set; } = string.Empty;
    [Required] public DateOnly IntendedLastWorkingDate { get; set; }
    [StringLength(4000)] public string? Comment { get; set; }
    public bool IsVoluntary { get; set; } = true;
}

public sealed class UpdateOffboardingRequest
{
    public DateOnly? AgreedLastWorkingDate { get; set; }
    public int? NoticePeriodDaysAdjustment { get; set; }
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(4000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class UpdateClearanceTaskRequest
{
    [Required, StringLength(20)] public string State { get; set; } = string.Empty;
    [StringLength(2000)] public string? Comment { get; set; }
    public IReadOnlyList<string> EvidenceUploadIds { get; set; } = [];
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class CloseOffboardingRequest
{
    [StringLength(2000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}
