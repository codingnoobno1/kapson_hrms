using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

/// <summary>
/// Loans and salary advances. Reads need PAYROLL.LOAN.VIEW; creating, deciding, recording a
/// repayment and cancelling need PAYROLL.LOAN.MANAGE. Instalments are recovered through payroll.
/// </summary>
public interface ILoanService
{
    Task<LoanWorkspaceDto> GetWorkspaceAsync(
        MobileCallerContext caller, LoanQuery query, CancellationToken cancellationToken = default);

    Task<LoanDetailDto> GetDetailAsync(
        MobileCallerContext caller, string loanId, CancellationToken cancellationToken = default);

    /// <summary>The Advance Payment Ledger modal: month-by-month opening/closing balance for one voucher.</summary>
    Task<LoanLedgerDto> GetLedgerAsync(
        MobileCallerContext caller, string loanId, CancellationToken cancellationToken = default);

    Task<LoanListItemDto> CreateAsync(
        MobileCallerContext caller, SaveLoanRequest request, CancellationToken cancellationToken = default);

    Task<LoanListItemDto> DecideAsync(
        MobileCallerContext caller, LoanDecisionRequest request, CancellationToken cancellationToken = default);

    Task<LoanListItemDto> RecordRepaymentAsync(
        MobileCallerContext caller, RecordLoanRepaymentRequest request, CancellationToken cancellationToken = default);

    Task<LoanListItemDto> CancelAsync(
        MobileCallerContext caller, string loanId, string? note, CancellationToken cancellationToken = default);

    /// <summary>
    /// Instalments due in a payroll month, keyed by employee id. Internal to payroll generation, so
    /// it carries no permission check of its own.
    /// </summary>
    Task<IReadOnlyDictionary<long, IReadOnlyList<LoanRecoveryDto>>> GetDueRecoveriesAsync(
        string tenantKey, int year, int month, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the recoveries an approved payroll run deducted. Idempotent per loan per month.
    /// Internal to payroll approval.
    /// </summary>
    Task<int> RecordPayrollRecoveriesAsync(
        string tenantKey, long payrollRunId, int year, int month,
        IReadOnlyList<(long LoanId, decimal Amount)> recoveries, string? recordedBy,
        CancellationToken cancellationToken = default);
}
