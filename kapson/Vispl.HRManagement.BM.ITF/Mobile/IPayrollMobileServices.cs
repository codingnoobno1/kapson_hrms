using Vispl.HRManagement.Contracts.Mobile.Payroll;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.BM.ITF.Mobile;

public interface IMobilePayrollService
{
    Task<CursorPage<PayslipListItemDto>> ListPayslipsAsync(
        MobileCallerContext caller,
        PayslipListQuery query,
        CancellationToken ct);

    Task<PayslipDetailDto> GetPayslipAsync(
        MobileCallerContext caller,
        string payslipId,
        CancellationToken ct);

    Task<ProtectedFileTicketDto> GetPayslipTicketAsync(
        MobileCallerContext caller,
        string payslipId,
        CancellationToken ct);

    Task<TaxSummaryDto> GetTaxSummaryAsync(
        MobileCallerContext caller,
        TaxSummaryQuery query,
        CancellationToken ct);

    Task<TaxDeclarationDto> GetDeclarationAsync(
        MobileCallerContext caller,
        int? financialYear,
        CancellationToken ct);

    Task<MutationResultDto> SaveDeclarationAsync(
        MobileCallerContext caller,
        SaveTaxDeclarationRequest request,
        CancellationToken ct);

    Task<MutationResultDto> SubmitDeclarationAsync(
        MobileCallerContext caller,
        SubmitTaxDeclarationRequest request,
        CancellationToken ct);

    Task<MutationResultDto> VerifyDeclarationLineAsync(
        MobileCallerContext caller,
        string employeeId,
        VerifyTaxDeclarationLineRequest request,
        CancellationToken ct);

    Task<SalaryStructureDto> GetSalaryStructureAsync(
        MobileCallerContext caller,
        string? employeeId,
        CancellationToken ct);
}

public interface IReimbursementService
{
    Task<CursorPage<ReimbursementListItemDto>> ListAsync(
        MobileCallerContext caller,
        ReimbursementListQuery query,
        CancellationToken ct);

    Task<ReimbursementDetailDto> GetAsync(
        MobileCallerContext caller,
        string claimId,
        CancellationToken ct);

    Task<MutationResultDto> SaveDraftAsync(
        MobileCallerContext caller,
        string? claimId,
        SaveReimbursementRequest request,
        CancellationToken ct);

    Task<MutationResultDto> SubmitAsync(
        MobileCallerContext caller,
        string claimId,
        SubmitReimbursementRequest request,
        CancellationToken ct);

    Task<MutationResultDto> WithdrawAsync(
        MobileCallerContext caller,
        string claimId,
        CancellationToken ct);

    Task<MutationResultDto> DecideAsync(
        MobileCallerContext caller,
        string claimId,
        ReimbursementDecisionRequest request,
        CancellationToken ct);

    Task<UploadIntentDto> CreateReceiptIntentAsync(
        MobileCallerContext caller,
        string claimId,
        ReimbursementReceiptRequest request,
        CancellationToken ct);
}

public interface IBenefitService
{
    Task<CursorPage<BenefitPlanDto>> ListPlansAsync(
        MobileCallerContext caller,
        BenefitPlansQuery query,
        CancellationToken ct);

    Task<BenefitComparisonDto> CompareAsync(
        MobileCallerContext caller,
        BenefitComparisonRequest request,
        CancellationToken ct);

    Task<BenefitCoverageDto> GetCoverageAsync(MobileCallerContext caller, CancellationToken ct);

    Task<BenefitEnrollmentResultDto> EnrolAsync(
        MobileCallerContext caller,
        SubmitBenefitEnrollmentRequest request,
        CancellationToken ct);
}
