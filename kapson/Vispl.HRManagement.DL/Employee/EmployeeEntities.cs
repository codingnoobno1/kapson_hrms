namespace Vispl.HRManagement.DL.Employee;

public sealed class EmployeeEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;

    /// <summary>Unique within a tenant. The number HR and payroll refer to.</summary>
    public string EmployeeNumber { get; set; } = string.Empty;

    // Personal
    public string LegalFirstName { get; set; } = string.Empty;
    public string? LegalMiddleName { get; set; }
    public string LegalLastName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? GenderCode { get; set; }
    public string? MaritalStatusCode { get; set; }
    public string? NationalityCode { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? AvatarUrl { get; set; }

    // Employment
    public DateOnly JoinedOn { get; set; }
    public string EmploymentTypeCode { get; set; } = string.Empty;
    public string EmploymentStatusCode { get; set; } = "Active";
    public string? GradeCode { get; set; }
    public string? JobTitle { get; set; }
    public DateOnly? ProbationEndsOn { get; set; }

    /// <summary>Report Configuration's "Date of Leaving". Set on exit; null while still employed.</summary>
    public DateOnly? LeavingOn { get; set; }

    // Organisation. Scalar codes only, resolved against master data.
    public string? CompanyCode { get; set; }
    public string? DivisionCode { get; set; }
    public string DepartmentCode { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public long? ManagerEmployeeId { get; set; }
    public string? CostCentre { get; set; }

    /// <summary>Identity account, once the employee has one. Null until invited.</summary>
    public string? AccountId { get; set; }

    /// <summary>The applicant this employee was hired from, when they came through recruitment.</summary>
    public long? CandidateId { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class EmployeeContactEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }

    /// <summary>Emergency, Personal or Work.</summary>
    public string Kind { get; set; } = "Emergency";

    public string Name { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public bool IsPrimary { get; set; }
    public bool IsVerified { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class EmployeeDependantEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string RelationCode { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public bool IsBenefitEligible { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class EmployeeTimelineEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }

    /// <summary>Hired, Transferred, Promoted, StatusChanged, DetailsChanged.</summary>
    public string EventType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateOnly EffectiveOn { get; set; }
    public string? ActorId { get; set; }
    public string? ActorName { get; set; }
    public DateTimeOffset OccurredUtc { get; set; }
}

public sealed class EmployeeChangeRequestEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }

    /// <summary>Personal, Employment or Organisation.</summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>The requested field changes, as a JSON array of field/current/requested.</summary>
    public string ChangesJson { get; set; } = "[]";

    public string? Reason { get; set; }

    /// <summary>Pending, Approved or Rejected.</summary>
    public string Status { get; set; } = "Pending";

    public string? DecisionReason { get; set; }
    public string? RequestedBy { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset SubmittedUtc { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
