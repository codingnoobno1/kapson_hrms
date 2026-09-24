using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface IBankAccountRepository
{
    Task<IReadOnlyList<CompanyBankAccountRecord>> GetCompanyAccountsAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    /// <summary>Saving an account as default clears the flag on every other account of the tenant.</summary>
    Task<long> UpsertCompanyAccountAsync(CompanyBankAccountRecord account, CancellationToken cancellationToken = default);

    Task<bool> DeleteCompanyAccountAsync(
        string tenantKey, long accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeBankAccountRecord>> GetEmployeeAccountsAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    Task<EmployeeBankAccountRecord?> GetEmployeeAccountAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default);

    /// <summary>One salary account per employee: this replaces the employee's existing one.</summary>
    Task UpsertEmployeeAccountAsync(EmployeeBankAccountRecord account, CancellationToken cancellationToken = default);

    Task<bool> RemoveEmployeeAccountAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default);
}
