using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.VO.Admin;
using Vispl.HRManagement.VO.Employee;

namespace Vispl.HRManagement.BM.Admin;

public sealed class BankAccountService(
    IBankAccountRepository accounts,
    IEmployeeRepository employees,
    ISensitiveValueProtector protector) : IBankAccountService
{
    private const string ViewPermission = "PAYROLL.VIEW";
    private const string ManagePermission = "PAYROLL.MANAGE";

    // Separate purposes, so a company account number can never be unprotected as an employee's.
    private const string CompanyPurpose = "CompanyBankAccount.AccountNumber";
    private const string EmployeePurpose = "EmployeeBankAccount.AccountNumber";

    // ---- Company accounts -------------------------------------------------

    public async Task<IReadOnlyList<CompanyBankAccountDto>> GetCompanyAccountsAsync(
        MobileCallerContext caller, CancellationToken cancellationToken = default)
    {
        Require(caller, ViewPermission);

        return (await accounts.GetCompanyAccountsAsync(caller.TenantKey, cancellationToken))
            .Select(ToDto)
            .ToList();
    }

    public async Task<string> SaveCompanyAccountAsync(
        MobileCallerContext caller, SaveCompanyBankAccountRequest request, CancellationToken cancellationToken = default)
    {
        Require(caller, ManagePermission);

        var all = await accounts.GetCompanyAccountsAsync(caller.TenantKey, cancellationToken);
        CompanyBankAccountRecord? existing = null;

        if (!string.IsNullOrWhiteSpace(request.Id))
        {
            var id = ParseId(request.Id);
            existing = all.FirstOrDefault(account => account.Id == id)
                ?? throw new MobileNotFoundException("That company bank account no longer exists.");
        }

        var label = Required(request.AccountLabel, "ACCOUNT_LABEL_REQUIRED", "An account label is required.");
        if (all.Any(account => account.Id != existing?.Id &&
                               string.Equals(account.AccountLabel, label, StringComparison.OrdinalIgnoreCase)))
        {
            throw new MobileValidationException("ACCOUNT_LABEL_TAKEN", $"Another company account is already labelled '{label}'.");
        }

        var number = NormaliseAccountNumber(request.AccountNumber);
        if (existing is null && number is null)
        {
            throw new MobileValidationException("ACCOUNT_NUMBER_REQUIRED", "An account number is required for a new account.");
        }

        // An inactive account cannot be the default. The first active account becomes the default
        // on its own, so the Transfer to Bank picker always has something preselected.
        var hasOtherDefault = all.Any(account => account.Id != existing?.Id && account.IsDefault && account.IsActive);
        var isDefault = request.IsActive && (request.IsDefault || !hasOtherDefault);

        var record = new CompanyBankAccountRecord
        {
            Id = existing?.Id ?? 0,
            TenantKey = caller.TenantKey,
            BankName = Required(request.BankName, "BANK_NAME_REQUIRED", "A bank name is required."),
            AccountLabel = label,
            AccountNumberProtected = number is null ? existing!.AccountNumberProtected : protector.Protect(CompanyPurpose, number),
            AccountLast4 = number is null ? existing!.AccountLast4 : number[^4..],
            Ifsc = NormaliseIfsc(request.Ifsc),
            BranchName = string.IsNullOrWhiteSpace(request.BranchName) ? null : request.BranchName.Trim(),
            IsDefault = isDefault,
            IsActive = request.IsActive,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        var savedId = await accounts.UpsertCompanyAccountAsync(record, cancellationToken);
        return savedId.ToString();
    }

    public async Task<bool> DeleteCompanyAccountAsync(
        MobileCallerContext caller, string accountId, CancellationToken cancellationToken = default)
    {
        Require(caller, ManagePermission);
        return await accounts.DeleteCompanyAccountAsync(caller.TenantKey, ParseId(accountId), cancellationToken);
    }

    // ---- Employee salary accounts -----------------------------------------

    public async Task<IReadOnlyList<EmployeeBankAccountRowDto>> GetEmployeeAccountsAsync(
        MobileCallerContext caller, CancellationToken cancellationToken = default)
    {
        Require(caller, ViewPermission);

        var people = await employees.QueryAsync(
            caller.TenantKey, new EmployeeQueryFilter(null, null, null, null, null), cancellationToken);
        var byEmployee = (await accounts.GetEmployeeAccountsAsync(caller.TenantKey, cancellationToken))
            .ToDictionary(account => account.EmployeeId);

        return people
            .OrderBy(person => person.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(person => person.EmployeeNumber, StringComparer.OrdinalIgnoreCase)
            .Select(person => new EmployeeBankAccountRowDto(
                person.Id.ToString(),
                person.EmployeeNumber,
                person.DisplayName,
                person.DivisionCode,
                person.IsActive,
                byEmployee.TryGetValue(person.Id, out var account) ? ToDetail(account) : null))
            .ToList();
    }

    public async Task<EmployeeBankAccountDetailDto?> GetEmployeeAccountAsync(
        MobileCallerContext caller, string employeeId, CancellationToken cancellationToken = default)
    {
        Require(caller, ViewPermission);

        var account = await accounts.GetEmployeeAccountAsync(caller.TenantKey, ParseId(employeeId), cancellationToken);
        return account is null ? null : ToDetail(account);
    }

    public async Task SaveEmployeeAccountAsync(
        MobileCallerContext caller, SaveEmployeeBankAccountRequest request, CancellationToken cancellationToken = default)
    {
        Require(caller, ManagePermission);

        var employee = await LoadEmployeeAsync(caller.TenantKey, request.EmployeeId, cancellationToken);
        var existing = await accounts.GetEmployeeAccountAsync(caller.TenantKey, employee.Id, cancellationToken);

        var number = NormaliseAccountNumber(request.AccountNumber);
        if (existing is null && number is null)
        {
            throw new MobileValidationException("ACCOUNT_NUMBER_REQUIRED", "An account number is required for a new salary account.");
        }

        var accountType = BankAccountTypes.All.FirstOrDefault(type =>
                              string.Equals(type, request.AccountType?.Trim(), StringComparison.OrdinalIgnoreCase))
                          ?? throw new MobileValidationException(
                              "ACCOUNT_TYPE_UNKNOWN", $"Account type must be one of {string.Join(", ", BankAccountTypes.All)}.");

        await accounts.UpsertEmployeeAccountAsync(new EmployeeBankAccountRecord
        {
            TenantKey = caller.TenantKey,
            EmployeeId = employee.Id,
            BankName = Required(request.BankName, "BANK_NAME_REQUIRED", "A bank name is required."),
            AccountHolderName = Required(request.AccountHolderName, "ACCOUNT_HOLDER_REQUIRED", "The account holder's name is required."),
            AccountNumberProtected = number is null ? existing!.AccountNumberProtected : protector.Protect(EmployeePurpose, number),
            AccountLast4 = number is null ? existing!.AccountLast4 : number[^4..],
            Ifsc = NormaliseIfsc(request.Ifsc),
            AccountType = accountType,
            UpdatedBy = caller.UserId,
            UpdatedUtc = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    public async Task<bool> RemoveEmployeeAccountAsync(
        MobileCallerContext caller, string employeeId, CancellationToken cancellationToken = default)
    {
        Require(caller, ManagePermission);
        return await accounts.RemoveEmployeeAccountAsync(caller.TenantKey, ParseId(employeeId), cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, EmployeeBankAccountSummaryDto>> GetSummariesAsync(
        string tenantKey, CancellationToken cancellationToken = default) =>
        (await accounts.GetEmployeeAccountsAsync(tenantKey, cancellationToken))
        .ToDictionary(
            account => account.EmployeeId,
            account => new EmployeeBankAccountSummaryDto(account.BankName, Mask(account.AccountLast4)));

    public async Task<IReadOnlyDictionary<long, EmployeePayoutAccount>> GetPayoutAccountsAsync(
        string tenantKey, CancellationToken cancellationToken = default) =>
        (await accounts.GetEmployeeAccountsAsync(tenantKey, cancellationToken))
        .ToDictionary(
            account => account.EmployeeId,
            account => new EmployeePayoutAccount(
                account.BankName,
                account.AccountHolderName,
                protector.Unprotect(EmployeePurpose, account.AccountNumberProtected),
                account.Ifsc,
                account.AccountType));

    // ---- Helpers ----------------------------------------------------------

    /// <summary>"•••• 4821" — the last four digits only, per SM-18.</summary>
    public static string Mask(string last4) => $"•••• {last4}";

    private async Task<EmployeeRecord> LoadEmployeeAsync(string tenantKey, string employeeId, CancellationToken cancellationToken) =>
        await employees.GetAsync(tenantKey, ParseId(employeeId), cancellationToken)
        ?? throw new MobileNotFoundException("That employee no longer exists.");

    private static CompanyBankAccountDto ToDto(CompanyBankAccountRecord account) => new(
        account.Id.ToString(),
        account.BankName,
        account.AccountLabel,
        Mask(account.AccountLast4),
        account.Ifsc,
        account.BranchName,
        account.IsDefault,
        account.IsActive,
        account.UpdatedUtc);

    private static EmployeeBankAccountDetailDto ToDetail(EmployeeBankAccountRecord account) => new(
        account.BankName,
        account.AccountHolderName,
        Mask(account.AccountLast4),
        account.Ifsc,
        account.AccountType,
        account.UpdatedUtc);

    /// <summary>Digits only, 9 to 18 of them; blank means "not supplied".</summary>
    private static string? NormaliseAccountNumber(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = string.Concat(value.Where(character => !char.IsWhiteSpace(character)));
        if (digits.Length is < 9 or > 18 || !digits.All(char.IsAsciiDigit))
        {
            throw new MobileValidationException("ACCOUNT_NUMBER_INVALID", "Account number must be 9 to 18 digits.");
        }

        return digits;
    }

    private static string NormaliseIfsc(string? value)
    {
        var ifsc = (value ?? string.Empty).Trim().ToUpperInvariant();
        var valid = ifsc.Length == 11 &&
                    ifsc[..4].All(char.IsAsciiLetterUpper) &&
                    ifsc[4] == '0' &&
                    ifsc[5..].All(char.IsAsciiLetterOrDigit);

        return valid
            ? ifsc
            : throw new MobileValidationException("IFSC_INVALID", "IFSC must be 11 characters, like HDFC0001234.");
    }

    private static string Required(string? value, string code, string message) =>
        string.IsNullOrWhiteSpace(value) ? throw new MobileValidationException(code, message) : value.Trim();

    private static long ParseId(string? value) =>
        long.TryParse(value, out var id) && id > 0
            ? id
            : throw new MobileNotFoundException("That record no longer exists.");

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }
}
