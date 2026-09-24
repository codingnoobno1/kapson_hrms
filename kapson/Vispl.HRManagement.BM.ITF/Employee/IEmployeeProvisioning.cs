namespace Vispl.HRManagement.BM.ITF.Employee;

public sealed record HireRequest(
    string TenantKey,
    long CandidateId,
    string FullName,
    string? Email,
    string? Phone,
    DateOnly JoinedOn,
    string DepartmentCode,
    string LocationCode,
    string? JobTitle,
    string EmploymentTypeCode);

/// <summary>
/// The employee module's own entry point for turning a completed hire into an employee record.
///
/// Recruitment calls this rather than writing to employee tables, so the module boundary the
/// monolith depends on stays intact: recruitment knows an interface, not a schema.
/// </summary>
public interface IEmployeeProvisioning
{
    /// <summary>
    /// Creates the employee for a hired candidate, or returns the existing one. Idempotent, so
    /// a retried or replayed completion cannot produce two employees for one person.
    /// </summary>
    Task<long> HireAsync(HireRequest request, CancellationToken cancellationToken = default);
}
