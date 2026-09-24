using Vispl.HRManagement.Contracts.Mobile.Employee;

namespace Vispl.HRManagement.BM.ITF.Mobile;

/// <summary>
/// Whether an employee may sign in to the mobile app.
///
/// Hiring provisions an employee record, not a login. Granting one is a deliberate HR act:
/// it puts attendance, leave and salary data on someone's phone.
/// </summary>
public interface IEmployeeMobileAccessService
{
    Task<IReadOnlyList<EmployeeMobileAccessDto>> GetAsync(
        MobileCallerContext caller, MobileAccessQuery query, CancellationToken ct);

    /// <summary>
    /// Creates the employee-portal account. The temporary password comes back once, for the
    /// granting user to pass on; it is never readable again.
    /// </summary>
    Task<GrantMobileAccessResultDto> GrantAsync(
        MobileCallerContext caller, string employeeId, GrantMobileAccessRequest request, CancellationToken ct);

    /// <summary>Switches an existing grant on or off without deleting its history.</summary>
    Task<EmployeeMobileAccessDto> SetActiveAsync(
        MobileCallerContext caller, string employeeId, bool isActive, CancellationToken ct);
}
