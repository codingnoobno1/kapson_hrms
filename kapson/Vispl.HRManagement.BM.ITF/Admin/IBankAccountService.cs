using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

/// <summary>
/// Company bank accounts and employee salary accounts. Reads need PAYROLL.VIEW, writes need
/// PAYROLL.MANAGE; account numbers are stored encrypted and only ever returned masked.
/// </summary>
public interface IBankAccountService
{
    Task<IReadOnlyList<CompanyBankAccountDto>> GetCompanyAccountsAsync(
        MobileCallerContext caller, CancellationToken cancellationToken = default);

    Task<string> SaveCompanyAccountAsync(
        MobileCallerContext caller, SaveCompanyBankAccountRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteCompanyAccountAsync(
        MobileCallerContext caller, string accountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeBankAccountRowDto>> GetEmployeeAccountsAsync(
        MobileCallerContext caller, CancellationToken cancellationToken = default);

    Task<EmployeeBankAccountDetailDto?> GetEmployeeAccountAsync(
        MobileCallerContext caller, string employeeId, CancellationToken cancellationToken = default);

    Task SaveEmployeeAccountAsync(
        MobileCallerContext caller, SaveEmployeeBankAccountRequest request, CancellationToken cancellationToken = default);

    Task<bool> RemoveEmployeeAccountAsync(
        MobileCallerContext caller, string employeeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Masked accounts keyed by employee id, for payroll. Carries no permission check of its own:
    /// the payroll service calling it enforces its own.
    /// </summary>
    Task<IReadOnlyDictionary<long, EmployeeBankAccountSummaryDto>> GetSummariesAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Full, decrypted account details keyed by employee id — only for building a bank advice file.
    /// No permission check of its own: the payroll service requires PAYROLL.TRANSFER first.
    /// </summary>
    Task<IReadOnlyDictionary<long, EmployeePayoutAccount>> GetPayoutAccountsAsync(
        string tenantKey, CancellationToken cancellationToken = default);
}

public sealed record EmployeePayoutAccount(
    string BankName,
    string AccountHolderName,
    string AccountNumber,
    string Ifsc,
    string AccountType);
