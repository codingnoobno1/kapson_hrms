using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public sealed record EmployeeExplorerItemRecord(
    long Id,
    string EmployeeNumber,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? PreferredName,
    string? AvatarUrl,
    string? JobTitle,
    string DepartmentCode,
    string? SubDepartmentCode,
    string? DivisionCode,
    string LocationCode,
    string? GradeCode,
    DateOnly JoinedOn,
    string? Email,
    string? Phone,
    long? ManagerEmployeeId,
    string EmploymentStatusCode,
    bool IsActive,
    bool IsDraft);

public sealed class EmployeeWorkspaceAggregate
{
    public long EmployeeId { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public bool IsActive { get; set; } = true;
    public string EmploymentStatusCode { get; set; } = "Active";
    public DateOnly? LeavingOn { get; set; }

    public EmployeePersonalWorkspaceDto Personal { get; set; } = new();
    public string? AadhaarProtected { get; set; }

    public List<EmployeeEducationItemDto> Education { get; set; } = [];
    public List<EmployeePreviousCompanyItemDto> PreviousCompanies { get; set; } = [];

    public EmployeeBankWorkspaceDto Bank { get; set; } = new();
    public string? BankAccountNumberProtected { get; set; }
    public string? BankAccountLast4 { get; set; }

    public EmployeePfEsiWorkspaceDto PfEsi { get; set; } = new();
    public EmployeeSalaryWorkspaceDto Salary { get; set; } = new();
    public List<EmployeeDocumentItemDto> Documents { get; set; } = [];
    public EmployeeLeavingWorkspaceDto Leaving { get; set; } = new();

    public string? UpdatedBy { get; set; }
    public string? UpdatedByName { get; set; }
}

public interface IEmployeeWorkspaceRepository
{
    Task<IReadOnlyList<EmployeeExplorerItemRecord>> ListExplorerAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAllEmployeeNumbersAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    Task<EmployeeWorkspaceAggregate?> GetAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default);

    Task<string?> FindConflictAsync(
        string tenantKey,
        long? excludeEmployeeId,
        string employeeNumber,
        string? email,
        string? normalizedPhone,
        string? panNumber,
        CancellationToken cancellationToken = default);

    Task<long> SaveAsync(
        EmployeeWorkspaceAggregate aggregate, CancellationToken cancellationToken = default);
}
