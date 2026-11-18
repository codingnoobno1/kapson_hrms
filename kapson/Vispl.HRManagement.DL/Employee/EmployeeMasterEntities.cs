namespace Vispl.HRManagement.DL.Employee;

// Employee master extensions taken from the HrManagemtNewDb29062026 scope script:
// statutory IDs, PF/ESI, education, previous employment and addresses. Each row belongs to
// one employee (employee.Employees.Id) within one tenant.

/// <summary>
/// Statutory identity and PF/ESI enrolment — one row per employee.
/// Aadhaar is stored like bank account numbers: encrypted, with the last four digits kept in
/// clear for display. PAN, UAN and ESI numbers are kept in clear with format checks; they are
/// needed for statutory filings and are not secret on their own.
/// </summary>
public sealed class EmployeeStatutoryEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }

    /// <summary>10 characters: AAAAA9999A.</summary>
    public string? PanNumber { get; set; }

    /// <summary>Encrypted with Data Protection; never stored in clear.</summary>
    public string? AadhaarProtected { get; set; }
    public string? AadhaarLast4 { get; set; }

    public string? VoterId { get; set; }

    public bool PfApplicable { get; set; }
    public DateOnly? PfJoinedOn { get; set; }
    /// <summary>Govt or Company.</summary>
    public string? PfScheme { get; set; }
    public string? PfNumber { get; set; }
    /// <summary>12-digit Universal Account Number.</summary>
    public string? UanNumber { get; set; }

    public bool EsiApplicable { get; set; }
    /// <summary>17-digit ESI insurance number.</summary>
    public string? EsiNumber { get; set; }

    public string? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

/// <summary>One qualification. Codes refer to the EducationLevel / Qualification masters.</summary>
public sealed class EmployeeEducationEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public string? EducationLevelCode { get; set; }
    public string? QualificationCode { get; set; }
    public string? Specialisation { get; set; }
    public string Institute { get; set; } = string.Empty;
    public string? BoardOrUniversity { get; set; }
    public int PassingYear { get; set; }
    public decimal? Percentage { get; set; }
    /// <summary>Out of 10.</summary>
    public decimal? Cgpa { get; set; }
    public string? GradeLetter { get; set; }
    public bool IsHighest { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>A job held before joining. LeavingReasonCode refers to the LeavingReason master.</summary>
public sealed class EmployeePreviousEmploymentEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? PreviousEmployeeId { get; set; }
    public DateOnly JoinedOn { get; set; }
    public DateOnly LeftOn { get; set; }
    /// <summary>Permanent, Contract, Intern or Consultant.</summary>
    public string? EmploymentType { get; set; }
    public decimal? LastAnnualCtc { get; set; }
    public string? ReportingManagerName { get; set; }
    public string? LeavingReasonCode { get; set; }
    public bool? NoticeServed { get; set; }
    public string? Remarks { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>Permanent or current address — at most one of each per employee.</summary>
public sealed class EmployeeAddressEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    /// <summary>Permanent or Current.</summary>
    public string AddressType { get; set; } = "Permanent";
    public string Line1 { get; set; } = string.Empty;
    public string? Line2 { get; set; }
    /// <summary>Codes refer to the City / State / Country masters.</summary>
    public string? CityCode { get; set; }
    public string? StateCode { get; set; }
    public string? CountryCode { get; set; }
    public string? Pincode { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
