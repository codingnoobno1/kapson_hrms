using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

/// <summary>
/// Loan and Advance (Salary Management → "Loan and advanced voucher"). No reference layout exists
/// (scopedocs/web-portal/10 §1, "named only"); these shapes follow the platform's payroll rules.
/// </summary>
public static class LoanTypes
{
    /// <summary>A loan: optional flat interest, repaid over several months.</summary>
    public const string Loan = "Loan";

    /// <summary>A salary advance: interest-free, usually recovered in one or a few months.</summary>
    public const string Advance = "Advance";

    public static IReadOnlyList<string> All { get; } = [Loan, Advance];
}

public static class LoanStatuses
{
    public const string Pending = "Pending";
    public const string Active = "Active";
    public const string Rejected = "Rejected";
    public const string Closed = "Closed";
    public const string Cancelled = "Cancelled";
}

public static class LoanRepaymentSources
{
    public const string Payroll = "Payroll";
    public const string Manual = "Manual";
}

public sealed class LoanQuery
{
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? DivisionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public string? DesignationName { get; set; }
    public string? EmployeeNumber { get; set; }
    public decimal? AmountMin { get; set; }
    public decimal? AmountMax { get; set; }
    public DateOnly? RequestedFrom { get; set; }
    public DateOnly? RequestedTo { get; set; }
    public string? Search { get; set; }
}

public sealed record LoanListItemDto(
    string Id,
    string VoucherNo,
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string DivisionName,
    string DepartmentName,
    string Type,
    decimal Principal,
    decimal InterestPercent,
    decimal TotalRepayable,
    int Installments,
    decimal MonthlyInstallment,
    int StartYear,
    int StartMonth,
    string StartMonthLabel,
    decimal Recovered,
    decimal Outstanding,
    int InstallmentsRecovered,
    string Status,
    string? Purpose,
    string? AdvanceTypeCode,
    string? AdvanceTypeName,
    string? Designation,
    DateOnly RequestedOn,
    DateOnly? DisbursedOn,
    DateTimeOffset? DecidedUtc,
    string? DecisionNote);

public sealed record LoanScheduleRowDto(
    int Number,
    int Year,
    int Month,
    string MonthLabel,
    decimal Due,
    decimal Recovered,
    decimal BalanceAfter,
    string State);

public sealed record LoanRepaymentDto(
    string Id,
    int Year,
    int Month,
    string MonthLabel,
    decimal Amount,
    string Source,
    DateOnly RecordedOn,
    string? Note);

public sealed record LoanDetailDto(
    LoanListItemDto Loan,
    IReadOnlyList<LoanScheduleRowDto> Schedule,
    IReadOnlyList<LoanRepaymentDto> Repayments);

/// <summary>The Loan and Advance Report's six KPI tiles — loans and advances counted separately.</summary>
public sealed record LoanSummaryDto(
    int ActiveCount,
    int PendingCount,
    decimal TotalPrincipal,
    decimal TotalOutstanding,
    decimal RecoveredThisMonth,

    /// <summary>Loan principal only (Type = Loan), active or closed.</summary>
    decimal TotalLoanAmount,

    /// <summary>Loan outstanding only (Type = Loan, Active).</summary>
    decimal OutstandingLoan,

    /// <summary>Loan repayments recorded to date (Type = Loan).</summary>
    decimal LoanRecovered,

    /// <summary>Advance principal only (Type = Advance), active or closed.</summary>
    decimal TotalAdvanceGiven,

    /// <summary>Advance outstanding only (Type = Advance, Active or Pending).</summary>
    decimal PendingAdvance,

    /// <summary>Distinct employees holding at least one loan or advance in scope.</summary>
    int EmployeesWithLoan);

/// <summary>One row of the Summary Report tab: an employee's loans and advances rolled into one line.</summary>
public sealed record LoanSummaryRowDto(
    string EmployeeId,
    string EmployeeNumber,
    string DisplayName,
    string DepartmentName,
    decimal TotalLoan,
    decimal LoanOutstanding,
    decimal AdvancePending,
    decimal TotalDeduction,
    string LoanStatus,
    string AdvanceStatus);

/// <summary>The Advance Payment Ledger modal: one loan/advance's month-by-month recovery.</summary>
public sealed record LoanLedgerRowDto(
    int Number,
    string MonthLabel,
    decimal OpeningBalance,
    decimal MonthlyDeduction,
    decimal AmountPaid,
    decimal ClosingBalance,
    string Status);

public sealed record LoanLedgerDto(
    LoanListItemDto Loan,
    string? DesignationName,
    IReadOnlyList<LoanLedgerRowDto> Rows);

public sealed record LoanWorkspaceDto(
    LoanSummaryDto Summary,
    IReadOnlyList<LoanListItemDto> Loans,
    IReadOnlyList<LoanSummaryRowDto> SummaryRows);

public sealed class SaveLoanRequest
{
    [Required] public string EmployeeId { get; set; } = string.Empty;
    [Required] public string Type { get; set; } = LoanTypes.Loan;

    [Range(typeof(decimal), "1", "10000000", ErrorMessage = "Amount must be between ₹1 and ₹1,00,00,000.")]
    public decimal Principal { get; set; }

    /// <summary>Flat annual interest. Always 0 for an advance.</summary>
    [Range(typeof(decimal), "0", "36", ErrorMessage = "Interest must be between 0% and 36% a year.")]
    public decimal InterestPercent { get; set; }

    [Range(1, 120, ErrorMessage = "Instalments must be between 1 and 120 months.")]
    public int Installments { get; set; } = 1;

    /// <summary>Advance only: the Advance Type master code. Ignored for a loan.</summary>
    [StringLength(40)] public string? AdvanceTypeCode { get; set; }

    [Range(1, 12)] public int StartMonth { get; set; } = DateTime.Today.Month;
    [Range(2000, 2100)] public int StartYear { get; set; } = DateTime.Today.Year;

    public DateOnly? DisbursedOn { get; set; }

    [StringLength(300)] public string? Purpose { get; set; }
}

public sealed class LoanDecisionRequest
{
    [Required] public string LoanId { get; set; } = string.Empty;
    public bool Approve { get; set; }
    [StringLength(300)] public string? Note { get; set; }
}

public sealed class RecordLoanRepaymentRequest
{
    [Required] public string LoanId { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "10000000", ErrorMessage = "Enter the amount repaid.")]
    public decimal Amount { get; set; }

    public DateOnly RecordedOn { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    [StringLength(300)] public string? Note { get; set; }
}

/// <summary>One instalment payroll deducts for a month. Internal to payroll generation.</summary>
public sealed record LoanRecoveryDto(long LoanId, string VoucherNo, string Type, decimal Amount);
