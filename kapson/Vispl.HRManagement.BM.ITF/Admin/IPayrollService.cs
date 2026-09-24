using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

/// <summary>
/// Generate Employee Salary (S06–S11). View needs PAYROLL.VIEW; generate and mark-as-paid need
/// PAYROLL.MANAGE; approval needs PAYROLL.APPROVE; bank transfer and advice need PAYROLL.TRANSFER.
/// </summary>
public interface IPayrollService
{
    Task<PayrollWorkspaceDto> GetWorkspaceAsync(
        MobileCallerContext caller, PayrollScopeQuery scope, CancellationToken cancellationToken = default);

    Task<PayrollRunResultDto> GenerateAsync(
        MobileCallerContext caller, GeneratePayrollRequest request, CancellationToken cancellationToken = default);

    Task ApproveAsync(
        MobileCallerContext caller, PayrollScopeQuery scope, CancellationToken cancellationToken = default);

    Task<MarkSalaryPaidResultDto> MarkPaidAsync(
        MobileCallerContext caller, MarkSalaryPaidRequest request, CancellationToken cancellationToken = default);

    Task<BankTransferResultDto> ExecuteTransferAsync(
        MobileCallerContext caller, ExecuteBankTransferRequest request, CancellationToken cancellationToken = default);

    Task<BankAdviceDto> BuildAdviceAsync(
        MobileCallerContext caller, ExecuteBankTransferRequest request, CancellationToken cancellationToken = default);
}
