using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

/// <summary>The account types an employee salary account may be recorded as.</summary>
public static class BankAccountTypes
{
    public const string Savings = "Savings";
    public const string Salary = "Salary";
    public const string Current = "Current";

    public static IReadOnlyList<string> All { get; } = [Savings, Salary, Current];
}

/// <summary>
/// One of the company's own accounts salaries are paid from — the "Bank Account" picker on the
/// Transfer to Bank modal (scopedocs/web-portal/10-salary-module-dynamic-pages-and-api.md §4).
/// The account number never leaves the service in full; only its last four digits do.
/// </summary>
public sealed record CompanyBankAccountDto(
    string Id,
    string BankName,
    string AccountLabel,
    string MaskedAccountNumber,
    string Ifsc,
    string? BranchName,
    bool IsDefault,
    bool IsActive,
    DateTimeOffset UpdatedUtc);

public sealed class SaveCompanyBankAccountRequest
{
    public string? Id { get; set; }

    [Required, StringLength(80)]
    public string BankName { get; set; } = string.Empty;

    /// <summary>How the account reads in pickers, e.g. "SBI - KAPSONS0034".</summary>
    [Required, StringLength(60)]
    public string AccountLabel { get; set; } = string.Empty;

    /// <summary>Required when adding. Left blank on an edit, the stored number is kept.</summary>
    [RegularExpression(@"^\s*\d{9,18}\s*$", ErrorMessage = "Account number must be 9 to 18 digits.")]
    public string? AccountNumber { get; set; }

    [Required, RegularExpression(@"^\s*[A-Za-z]{4}0[A-Za-z0-9]{6}\s*$", ErrorMessage = "IFSC must look like SBIN0001234.")]
    public string Ifsc { get; set; } = string.Empty;

    [StringLength(120)]
    public string? BranchName { get; set; }

    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>The employee's own account as payroll shows it: bank and last four digits (SM-18).</summary>
public sealed record EmployeeBankAccountSummaryDto(
    string BankName,
    string MaskedAccountNumber);

public sealed record EmployeeBankAccountDetailDto(
    string BankName,
    string AccountHolderName,
    string MaskedAccountNumber,
    string Ifsc,
    string AccountType,
    DateTimeOffset UpdatedUtc);

/// <summary>An employee, and their salary account if one is recorded.</summary>
public sealed record EmployeeBankAccountRowDto(
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string? DivisionCode,
    bool IsActive,
    EmployeeBankAccountDetailDto? Account);

public sealed class SaveEmployeeBankAccountRequest
{
    [Required]
    public string EmployeeId { get; set; } = string.Empty;

    [Required, StringLength(80)]
    public string BankName { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string AccountHolderName { get; set; } = string.Empty;

    /// <summary>Required when adding. Left blank on an edit, the stored number is kept.</summary>
    [RegularExpression(@"^\s*\d{9,18}\s*$", ErrorMessage = "Account number must be 9 to 18 digits.")]
    public string? AccountNumber { get; set; }

    [Required, RegularExpression(@"^\s*[A-Za-z]{4}0[A-Za-z0-9]{6}\s*$", ErrorMessage = "IFSC must look like HDFC0001234.")]
    public string Ifsc { get; set; } = string.Empty;

    [Required]
    public string AccountType { get; set; } = BankAccountTypes.Savings;
}
