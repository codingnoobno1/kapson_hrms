using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Employee;

public sealed record EmployeeListItemDto(
    string Id,
    string EmployeeNumber,
    string DisplayName,
    string? AvatarUrl,
    string JobTitle,
    string Department,
    string Location,
    StatusDto Status,
    string? Presence);

public sealed record EmployeeOverviewDto(
    EmployeeListItemDto Summary,
    PersonReferenceDto? Manager,
    DateOnly? JoinedOn,
    IReadOnlyList<string> AvailableSections,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);

public sealed record EmployeePersonalDto(
    string EmployeeId,
    string LegalName,
    string? PreferredName,
    DateOnly? DateOfBirth,
    string? GenderCode,
    string? MaritalStatusCode,
    string? Email,
    string? Phone,
    IReadOnlyList<FieldAccessDto> FieldAccess,
    string? RowVersion);

public sealed record EmployeeEmploymentDto(
    string EmployeeId,
    string EmployeeNumber,
    DateOnly JoinedOn,
    string EmploymentTypeCode,
    string EmploymentStatusCode,
    string? GradeCode,
    DateOnly? ProbationEndsOn,
    string? RowVersion);

public sealed record EmployeeOrganisationDto(
    string EmployeeId,
    string? CompanyCode,
    string? DivisionCode,
    string DepartmentCode,
    string LocationCode,
    PersonReferenceDto? Manager,
    string? CostCentre,
    string? RowVersion);

public sealed record CompensationSnapshotDto(
    string EmployeeId,
    DateOnly EffectiveFrom,
    MoneyDto? AnnualTotal,
    IReadOnlyList<PayComponentDto> Components,
    bool CanViewHistory);

public sealed record EmployeeContactDto(
    string Id,
    string Type,
    string Value,
    bool IsPrimary,
    bool IsVerified,
    string? RowVersion);

public sealed record EmployeeDependantDto(
    string Id,
    string DisplayName,
    string RelationCode,
    DateOnly? DateOfBirth,
    bool IsBenefitEligible,
    string? RowVersion);

public sealed record EmployeeTimelineEntryDto(
    string Id,
    string EventType,
    string Title,
    DateOnly EffectiveOn,
    string Summary,
    PersonReferenceDto? Actor);

public sealed record EmployeeDocumentDto(
    string Id,
    string CategoryCode,
    AttachmentMetadataDto File,
    StatusDto VerificationStatus,
    DateOnly? ExpiresOn,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);

public sealed record OnboardingSummaryDto(
    string Id,
    PersonReferenceDto Employee,
    DateOnly JoiningOn,
    string Stage,
    decimal CompletionPercent,
    int OverdueTaskCount,
    PersonReferenceDto? Owner);

public sealed record OnboardingTaskDetailDto(
    string Id,
    string Title,
    string Instructions,
    PersonReferenceDto? Owner,
    DateTimeOffset? DueUtc,
    StatusDto Status,
    IReadOnlyList<AttachmentMetadataDto> Evidence,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);

public sealed record ProbationDetailDto(
    string EmployeeId,
    DateOnly StartsOn,
    DateOnly EndsOn,
    IReadOnlyList<string> Goals,
    string? Recommendation,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);

public sealed record OffboardingDetailDto(
    string Id,
    PersonReferenceDto Employee,
    string ExitTypeCode,
    string LeavingReasonCode,
    DateOnly LastWorkingDay,
    StatusDto Status,
    IReadOnlyList<ClearanceTaskDto> ClearanceTasks,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);

public sealed record ClearanceTaskDto(
    string Id,
    string Title,
    PersonReferenceDto? Owner,
    DateTimeOffset? DueUtc,
    StatusDto Status,
    string? RowVersion);

public sealed record PayComponentDto(
    string Code,
    string Name,
    MoneyDto Amount,
    string Category,
    bool IsTaxable,
    MoneyDto? YearToDate);

public sealed class CreateEmployeeRequest
{
    [Required, StringLength(160)] public string LegalName { get; set; } = string.Empty;
    [Required, EmailAddress, StringLength(256)] public string Email { get; set; } = string.Empty;
    [Required, StringLength(60)] public string EmployeeNumber { get; set; } = string.Empty;
    [Required] public DateOnly JoinedOn { get; set; }
    [Required, StringLength(80)] public string DepartmentCode { get; set; } = string.Empty;
    [Required, StringLength(80)] public string LocationCode { get; set; } = string.Empty;
    public IReadOnlyList<string> RoleIds { get; set; } = [];

    // Optional: the web portal's Add New Employee form captures these up front. Codes are
    // validated against the masters when supplied; leaving them out keeps the mobile contract.
    [StringLength(30)] public string? Phone { get; set; }
    [StringLength(80)] public string? GenderCode { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    [StringLength(80)] public string? DivisionCode { get; set; }
    [StringLength(80)] public string? GradeCode { get; set; }
    [StringLength(160)] public string? JobTitle { get; set; }
    public string? ManagerEmployeeId { get; set; }
    [StringLength(40)] public string? EmploymentType { get; set; }
    public DateOnly? ProbationEndsOn { get; set; }
}

public sealed record CreateEmployeeResultDto(
    string EmployeeId,
    string? AccountId,
    string? OnboardingId,
    string InvitationState);
