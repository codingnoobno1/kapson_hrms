using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface ILoanRepository
{
    Task<IReadOnlyList<LoanRecord>> GetLoansAsync(string tenantKey, CancellationToken cancellationToken = default);

    Task<LoanRecord?> GetLoanAsync(string tenantKey, long loanId, CancellationToken cancellationToken = default);

    /// <summary>Inserts a loan and assigns its voucher number ("LA-2026-0001"), unique per tenant.</summary>
    Task<LoanRecord> InsertLoanAsync(LoanRecord loan, CancellationToken cancellationToken = default);

    Task UpdateLoanAsync(LoanRecord loan, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoanRepaymentRecord>> GetRepaymentsAsync(
        string tenantKey, long? loanId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a repayment. A payroll recovery is recorded at most once per loan per month; a repeat
    /// returns false instead of recording the money twice.
    /// </summary>
    Task<bool> AddRepaymentAsync(LoanRepaymentRecord repayment, CancellationToken cancellationToken = default);
}
