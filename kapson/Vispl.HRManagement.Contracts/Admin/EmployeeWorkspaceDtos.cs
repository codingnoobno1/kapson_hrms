using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

public sealed class EmployeeExplorerQuery
{
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? Status { get; set; }
    public string? Search { get; set; }
}

public sealed record EmployeeExplorerRowDto(
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string? AvatarUrl,
    string? Designation,
    string DepartmentCode,
    string DepartmentName,
    string? SubDepartmentName,
    string DivisionCode,
    string DivisionName,
    string LocationName,
    string? GradeCode,
    string? GradeName,
    DateOnly JoinedOn,
    string? Email,
    string? Phone,
    string? ManagerName,
    string StatusLabel,
    string StatusTone,
    bool IsActive,
    bool IsDraft);

public sealed record UploadedEmployeeMediaDto(
    long MediaAssetId,
    string FileName,
    string PublicUrl);

public sealed class EmployeeWorkspaceDto
{
    public string? EmployeeId { get; set; }
    public bool IsDraft { get; set; }

    public EmployeePersonalWorkspaceDto Personal { get; set; } = new();
    public List<EmployeeEducationItemDto> Education { get; set; } = [];
    public List<EmployeePreviousCompanyItemDto> PreviousCompanies { get; set; } = [];
    public EmployeeBankWorkspaceDto Bank { get; set; } = new();
    public EmployeePfEsiWorkspaceDto PfEsi { get; set; } = new();
    public EmployeeSalaryWorkspaceDto Salary { get; set; } = new();
    public List<EmployeeDocumentItemDto> Documents { get; set; } = [];
    public EmployeeLeavingWorkspaceDto Leaving { get; set; } = new();
}

public sealed class EmployeePersonalWorkspaceDto
{
    [StringLength(60)]
    public string EmployeeNumber { get; set; } = string.Empty;

    [StringLength(80)]
    public string FirstName { get; set; } = string.Empty;

    [StringLength(80)]
    public string? MiddleName { get; set; }

    [StringLength(80)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(256)]
    public string Email { get; set; } = string.Empty;

    [StringLength(30)]
    public string Mobile { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }
    public DateOnly JoinedOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(40)]
    public string? GenderCode { get; set; }

    [StringLength(500)]
    public string? AvatarUrl { get; set; }

    // Organisation & Placement
    [StringLength(80)]
    public string DepartmentCode { get; set; } = string.Empty;

    [StringLength(80)]
    public string? SubDepartmentCode { get; set; }

    [StringLength(80)]
    public string? DesignationCode { get; set; }

    [StringLength(160)]
    public string? JobTitle { get; set; }

    [StringLength(80)]
    public string? DivisionCode { get; set; }

    [StringLength(80)]
    public string LocationCode { get; set; } = string.Empty;

    public string? ReportingManagerId { get; set; }

    // Personal & Health
    [StringLength(40)]
    public string? MaritalStatusCode { get; set; }

    [StringLength(40)]
    public string? BloodGroupCode { get; set; }

    [StringLength(40)]
    public string? ReligionCode { get; set; }

    [StringLength(100)]
    public string? OtherReligion { get; set; }

    [StringLength(80)]
    public string? NationalityCode { get; set; }

    // Family Details
    [StringLength(120)]
    public string? FatherName { get; set; }

    [StringLength(40)]
    public string? FatherOccupationCode { get; set; }

    [StringLength(120)]
    public string? MotherName { get; set; }

    [StringLength(40)]
    public string? MotherOccupationCode { get; set; }

    [StringLength(120)]
    public string? SpouseName { get; set; }

    [StringLength(40)]
    public string? SpouseOccupationCode { get; set; }

    // Statutory Identity
    [StringLength(10)]
    public string? PanNumber { get; set; }

    [StringLength(16)]
    public string? AadhaarNumber { get; set; }

    public string? AadhaarLast4 { get; set; }

    [StringLength(20)]
    public string? VoterId { get; set; }

    // Addresses
    public EmployeeAddressItemDto PermanentAddress { get; set; } = new() { AddressType = "Permanent" };
    public EmployeeAddressItemDto CurrentAddress { get; set; } = new() { AddressType = "Current" };
    public bool SameAsPermanentAddress { get; set; }
}

public sealed class EmployeeAddressItemDto
{
    public string AddressType { get; set; } = "Permanent";

    [StringLength(200)]
    public string Line1 { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Line2 { get; set; }

    [StringLength(64)]
    public string? CountryCode { get; set; }

    [StringLength(64)]
    public string? StateCode { get; set; }

    [StringLength(64)]
    public string? CityCode { get; set; }

    [StringLength(10)]
    public string? Pincode { get; set; }
}

public sealed class EmployeeEducationItemDto
{
    public long Id { get; set; }
    public string? QualificationCode { get; set; }
    public string? EducationLevelCode { get; set; }
    public string? Specialisation { get; set; }
    public string Institute { get; set; } = string.Empty;
    public string? BoardOrUniversity { get; set; }
    public int PassingYear { get; set; } = DateTime.Today.Year;
    public decimal? Cgpa { get; set; }
    public string? GradeLetter { get; set; }
    public decimal? Percentage { get; set; }
    public string? CertificationName { get; set; }
    public DateOnly? CertificationValidTill { get; set; }
    public bool IsHighest { get; set; }
}

public sealed class EmployeePreviousCompanyItemDto
{
    public long Id { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? Department { get; set; }
    public string? PreviousEmployeeId { get; set; }
    public DateOnly JoinedOn { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddYears(-1));
    public DateOnly LeftOn { get; set; } = DateOnly.FromDateTime(DateTime.Today.AddDays(-1));
    public string? EmploymentType { get; set; } = "Permanent";
    public decimal? LastAnnualCtc { get; set; }
    public string? ReportingManagerName { get; set; }
    public string? LeavingReasonCode { get; set; }
    public bool? NoticeServed { get; set; } = true;
    public string? Remarks { get; set; }
}

public sealed class EmployeeBankWorkspaceDto
{
    [StringLength(120)]
    public string AccountHolderName { get; set; } = string.Empty;

    [StringLength(80)]
    public string BankName { get; set; } = string.Empty;

    [StringLength(20)]
    public string? AccountNumber { get; set; }

    public string? MaskedAccountNumber { get; set; }

    [StringLength(11)]
    public string Ifsc { get; set; } = string.Empty;

    [StringLength(120)]
    public string? BranchName { get; set; }

    [StringLength(20)]
    public string AccountType { get; set; } = BankAccountTypes.Savings;

    [StringLength(30)]
    public string? TransferSalarySlipBy { get; set; }

    [StringLength(30)]
    public string? SalaryFormat { get; set; }

    [StringLength(80)]
    public string? SalaryAccountCode { get; set; }

    [StringLength(80)]
    public string? AdvancedAccountCode { get; set; }
}

public sealed class EmployeePfEsiWorkspaceDto
{
    public bool PfApplicable { get; set; }
    public DateOnly? PfJoinedOn { get; set; }
    public string PfScheme { get; set; } = "Govt";
    public string? PfNumber { get; set; }
    public string? UanNumber { get; set; }
    public string? EpfAccountCode { get; set; }

    public bool EsiApplicable { get; set; }
    public string? EsiNumber { get; set; }
    public string? EsiAccountCode { get; set; }
}

public sealed class EmployeeSalaryWorkspaceDto
{
    public bool ApplyMaxLimit { get; set; } = true;
    public string EpfEmployerShareRule { get; set; } = "Apply EPF Employer Share on Maxlimit";
    public bool EpfOnBasicOnly { get; set; } = true;
    public bool ExcludeFromPayroll { get; set; }

    public string? GradeCode { get; set; }
    public decimal BasicSalary { get; set; }
    public List<EmployeeSalaryAllowanceRowDto> Components { get; set; } = [];

    public void Recalculate()
    {
        var basic = Math.Max(0m, BasicSalary);
        var gross = basic;

        foreach (var row in Components.Where(c => c.Kind == SalaryComponentKinds.Allowance))
        {
            row.CalculatedAmount = ComputeRowAmount(row, basic, basic);
            gross += row.CalculatedAmount;
        }

        foreach (var row in Components.Where(c => c.Kind != SalaryComponentKinds.Allowance))
        {
            row.CalculatedAmount = ComputeRowAmount(row, basic, gross);
        }
    }

    private static decimal ComputeRowAmount(EmployeeSalaryAllowanceRowDto row, decimal basic, decimal gross)
    {
        if (!row.IsPercentage)
        {
            return Math.Round(Math.Max(0m, row.Amount), 2);
        }

        var baseAmount = row.OnBasicOrGross switch
        {
            "Gross" => gross,
            "On Amount" => row.CalculatedAmount > 0 ? row.CalculatedAmount : basic,
            _ => basic
        };

        if (string.Equals(row.CalculationOn, "Amount", StringComparison.OrdinalIgnoreCase) && row.CalculatedAmount > 0)
        {
            return Math.Round(row.CalculatedAmount * row.Amount / 100m, 2);
        }

        return Math.Round(baseAmount * row.Amount / 100m, 2);
    }
}

public sealed class EmployeeSalaryAllowanceRowDto
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Kind { get; set; } = SalaryComponentKinds.Allowance;
    public bool IsPercentage { get; set; }
    public decimal Amount { get; set; }
    public string OnBasicOrGross { get; set; } = "Basic";
    public bool VaryOnDays { get; set; } = true;
    public string CalculationOn { get; set; } = "Salary";
    public decimal CalculatedAmount { get; set; }
}

public sealed class EmployeeDocumentItemDto
{
    public long Id { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string? DocumentNumber { get; set; }
    public DateOnly? IssuedOn { get; set; }
    public DateOnly? ExpiresOn { get; set; }
    public long? MediaAssetId { get; set; }
    public string? FileName { get; set; }
    public string? PublicUrl { get; set; }
}

public sealed class EmployeeLeavingWorkspaceDto
{
    public bool HasLeftOrganization { get; set; }
    public DateOnly? ResignationSubmitDate { get; set; }
    public DateOnly? FinalSettlementDate { get; set; }
    public string? LeavingReasonCode { get; set; }
    public string? LeavingRemarks { get; set; }

    public bool IsDismissed { get; set; }
    public DateOnly? DismissDate { get; set; }
    public string? DismissRemarks { get; set; }
    public DateOnly? SettlementDate { get; set; }
}
