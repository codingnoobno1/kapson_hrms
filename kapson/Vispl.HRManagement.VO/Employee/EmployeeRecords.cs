namespace Vispl.HRManagement.VO.Employee;

public sealed record EmployeeRecord(
    long Id,
    string TenantKey,
    string EmployeeNumber,
    string LegalFirstName,
    string? LegalMiddleName,
    string LegalLastName,
    string? PreferredName,
    DateOnly? DateOfBirth,
    string? GenderCode,
    string? MaritalStatusCode,
    string? NationalityCode,
    string? Email,
    string? Phone,
    string? AvatarUrl,
    DateOnly JoinedOn,
    string EmploymentTypeCode,
    string EmploymentStatusCode,
    string? GradeCode,
    string? JobTitle,
    DateOnly? ProbationEndsOn,
    string? CompanyCode,
    string? DivisionCode,
    string DepartmentCode,
    string LocationCode,
    long? ManagerEmployeeId,
    string? CostCentre,
    string? AccountId,
    long? CandidateId,
    bool IsActive,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc,

    /// <summary>Set on exit (offboarding, or HR directly). Null while still employed.</summary>
    DateOnly? LeavingOn = null)
{
    public string DisplayName => string.IsNullOrWhiteSpace(PreferredName)
        ? $"{LegalFirstName} {LegalLastName}".Trim()
        : PreferredName!;

    public string LegalName => string.Join(
        ' ',
        new[] { LegalFirstName, LegalMiddleName, LegalLastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
}

public sealed record EmployeeContactRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    string Kind,
    string Name,
    string? Relationship,
    string? Phone,
    string? Email,
    DateOnly? DateOfBirth,
    bool IsPrimary,
    bool IsVerified,
    DateTimeOffset UpdatedUtc);

public sealed record EmployeeDependantRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    string DisplayName,
    string RelationCode,
    DateOnly? DateOfBirth,
    bool IsBenefitEligible,
    DateTimeOffset UpdatedUtc);

public sealed record EmployeeTimelineRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    string EventType,
    string Title,
    string Summary,
    DateOnly EffectiveOn,
    string? ActorId,
    string? ActorName,
    DateTimeOffset OccurredUtc);

public sealed record EmployeeChangeRequestRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    string Section,
    string ChangesJson,
    string? Reason,
    string Status,
    string? DecisionReason,
    string? RequestedBy,
    string? DecidedBy,
    DateTimeOffset SubmittedUtc,
    DateTimeOffset? DecidedUtc);

public sealed record EmployeeQueryFilter(
    string? Search,
    string? DepartmentCode,
    string? LocationCode,
    string? StatusCode,
    long? ManagerEmployeeId,
    string? CompanyCode = null,
    string? UnitCode = null,
    bool ActiveOnly = false,

    /// <summary>
    /// Excludes anyone who had not started by this date.
    ///
    /// A person hired to start next month is a real employee record and belongs in the
    /// directory, but they are not part of any population measured today: counting them makes
    /// headcount wrong and, because absence is derived as rostered minus present, marks
    /// somebody absent from a job they have not begun.
    ///
    /// Null means no cut-off, which is what a directory wants.
    /// </summary>
    DateOnly? JoinedOnOrBefore = null);

/// <summary>
/// An employee whose birthday or work anniversary falls inside the window being shown.
/// </summary>
/// <param name="Date">The original date — of birth, or of joining.</param>
/// <param name="Years">Age being turned, or years of service completed.</param>
/// <param name="Window">Today, Belated or Upcoming.</param>
/// <summary>One company or unit the dashboard may be filtered to, with its headcount.</summary>
public sealed record ScopeOptionRecord(string Code, string Name, int EmployeeCount);

public sealed record PersonMomentRecord(
    long EmployeeId,
    string DisplayName,
    string? AvatarUrl,
    string? DepartmentCode,
    string? JobTitle,
    DateOnly Date,
    int Years,
    string Window);
