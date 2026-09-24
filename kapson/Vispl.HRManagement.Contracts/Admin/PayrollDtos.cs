using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

/// <summary>Where one employee's salary stands for a month (scopedocs/web-portal/08 §7–9).</summary>
public static class PayrollLineStatuses
{
    /// <summary>In scope, not generated yet. Never stored — it is the absence of a line.</summary>
    public const string Pending = "Pending";
    public const string Generated = "Generated";
    public const string Paid = "Paid";
    public const string Transferred = "Transferred";
}

public static class BankTransferModes
{
    public static IReadOnlyList<string> All { get; } = ["IMPS", "NEFT", "RTGS"];
}

public static class PayrollSalaryTypes
{
    public const string Monthly = "Monthly";
}

/// <summary>The Salary Generation Controls bar: the single source of scope for the whole page.</summary>
public sealed class PayrollScopeQuery
{
    [Range(1, 12)] public int Month { get; set; }
    [Range(2000, 2100)] public int Year { get; set; }
    [Required, StringLength(30)] public string SalaryType { get; set; } = PayrollSalaryTypes.Monthly;
    [StringLength(80)] public string? DivisionCode { get; set; }
    [StringLength(80)] public string? DepartmentCode { get; set; }
}

/// <summary>
/// Money for one employee or a total. Gross, Deductions and Net are always computed from their
/// parts on the server, so the aggregate columns can never disagree with the breakdown columns.
/// </summary>
public sealed record PayrollAmountsDto(
    decimal Basic,
    decimal Hra,
    decimal Conveyance,
    decimal OtherAllowances,
    decimal Gross,
    decimal Pf,
    decimal Esi,
    decimal Tds,
    decimal OtherDeductions,
    decimal Deductions,
    decimal NetPayable)
{
    public static PayrollAmountsDto Zero { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    public PayrollAmountsDto Add(PayrollAmountsDto other) => new(
        Basic + other.Basic,
        Hra + other.Hra,
        Conveyance + other.Conveyance,
        OtherAllowances + other.OtherAllowances,
        Gross + other.Gross,
        Pf + other.Pf,
        Esi + other.Esi,
        Tds + other.Tds,
        OtherDeductions + other.OtherDeductions,
        Deductions + other.Deductions,
        NetPayable + other.NetPayable);

    public static PayrollAmountsDto Sum(IEnumerable<PayrollAmountsDto> items) =>
        items.Aggregate(Zero, (total, item) => total.Add(item));
}

/// <summary>The four tiles. <c>TotalEmployees = Generated + Pending</c> always holds.</summary>
public sealed record PayrollSummaryDto(
    int TotalEmployees,
    int ActiveDivisionCount,
    decimal GrossPayroll,
    int Pending,
    int Generated);

public sealed record PayrollRunStateDto(
    string? RunId,
    bool IsApproved,
    DateTimeOffset? GeneratedUtc,
    DateTimeOffset? ApprovedUtc);

public sealed record EmployeePayrollRowDto(
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string DivisionCode,
    string DivisionName,
    string DepartmentCode,
    string DepartmentName,
    string Designation,
    string? ShiftCode,
    int WorkedDays,
    int ExpectedDays,
    PayrollAmountsDto Amounts,
    string Status,
    string? PendingReason,
    EmployeeBankAccountSummaryDto? BankAccount,
    DateOnly? PaidOn);

public sealed record DivisionPayrollRowDto(
    string DivisionCode,
    string DivisionName,
    int EmployeeCount,
    int DepartmentCount,
    int PendingCount,
    PayrollAmountsDto Totals,
    string Status);

public sealed record DepartmentPayrollRowDto(
    string DepartmentCode,
    string DepartmentName,
    string DivisionCode,
    string DivisionName,
    int EmployeeCount,
    int PendingCount,
    PayrollAmountsDto Totals,
    string Status);

public sealed record DivisionPayrollGroupDto(
    string DivisionCode,
    string DivisionName,
    IReadOnlyList<DepartmentPayrollRowDto> Departments,
    PayrollAmountsDto Subtotal);

/// <summary>Everything the Generate Employee Salary page shows for one scope, in one read.</summary>
public sealed record PayrollWorkspaceDto(
    int Month,
    int Year,
    string MonthLabel,
    string SalaryType,
    string TenantDisplayName,
    PayrollRunStateDto Run,
    PayrollSummaryDto Summary,
    IReadOnlyList<DivisionPayrollRowDto> Divisions,
    PayrollAmountsDto GrandTotal,
    IReadOnlyList<DivisionPayrollGroupDto> DepartmentGroups,
    IReadOnlyList<EmployeePayrollRowDto> Employees,
    int AlreadyTransferredThisMonth,
    IReadOnlyList<CompanyBankAccountDto> CompanyAccounts);

public sealed class GeneratePayrollRequest
{
    [Required] public PayrollScopeQuery Scope { get; set; } = new();

    /// <summary>Empty means everyone pending in scope ("Generate All"); otherwise only these.</summary>
    public IReadOnlyList<string> EmployeeIds { get; set; } = [];
}

public sealed record PayrollRunResultDto(
    string RunId,
    int Generated,
    int Skipped,
    IReadOnlyList<string> SkippedReasons);

public sealed class MarkSalaryPaidRequest
{
    [Required] public PayrollScopeQuery Scope { get; set; } = new();
    [Required, MinLength(1)] public IReadOnlyList<string> EmployeeIds { get; set; } = [];
    [Required] public DateOnly PaymentDate { get; set; }
    [Required, StringLength(30)] public string PaymentMode { get; set; } = "Bank Transfer";
    [Required, StringLength(30)] public string DocumentType { get; set; } = "Monthly Payroll";
}

public sealed record MarkSalaryPaidResultDto(int EmployeesUpdated, decimal TotalAmount);

public sealed class ExecuteBankTransferRequest
{
    [Required] public PayrollScopeQuery Scope { get; set; } = new();
    [Required, MinLength(1)] public IReadOnlyList<string> EmployeeIds { get; set; } = [];
    [Required] public DateOnly TransferDate { get; set; }
    [Required] public string CompanyBankAccountId { get; set; } = string.Empty;
    [Required] public string TransferMode { get; set; } = "IMPS";
}

/// <summary>The reference number is server-assigned, never accepted from the client (doc 10 §8).</summary>
public sealed record BankTransferResultDto(
    string BatchId,
    string ReferenceNo,
    decimal TotalAmount,
    int EmployeesTransferred);

/// <summary>A bank upload workbook (.xlsx) for selected employees, with full account numbers.</summary>
public sealed record BankAdviceDto(string FileName, byte[] Content, string ContentType, decimal TotalAmount, int Employees);
