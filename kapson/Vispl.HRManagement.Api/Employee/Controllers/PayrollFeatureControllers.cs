using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Payroll;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Employee.Controllers;

// MBK-028 — WF-007 -----------------------------------------------------------

[Route("api/mobile/v1/payroll")]
public sealed class PayrollController(IMobilePayrollService payroll) : MobileApiControllerBase
{
    /// <summary>Payslip metadata only. Amounts are never cached on the device.</summary>
    [HttpGet("payslips")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<PayslipListItemDto>>>> Payslips(
        [FromQuery] PayslipListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await payroll.ListPayslipsAsync(Caller, request, ct));

    [HttpGet("payslips/{payslipId}")]
    public async Task<ActionResult<ApiEnvelope<PayslipDetailDto>>> Payslip(
        string payslipId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await payroll.GetPayslipAsync(Caller, payslipId, ct));
    }

    [HttpPost("payslips/{payslipId}/ticket")]
    public async Task<ActionResult<ApiEnvelope<ProtectedFileTicketDto>>> PayslipTicket(
        string payslipId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await payroll.GetPayslipTicketAsync(Caller, payslipId, ct));
    }

    [HttpGet("tax")]
    public async Task<ActionResult<ApiEnvelope<TaxSummaryDto>>> Tax(
        [FromQuery] TaxSummaryQuery query,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await payroll.GetTaxSummaryAsync(Caller, query, ct));
    }

    // Declarations (WF-007) ---------------------------------------------------

    [HttpGet("tax/declarations")]
    public async Task<ActionResult<ApiEnvelope<TaxDeclarationDto>>> Declaration(
        [FromQuery] int? financialYear,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await payroll.GetDeclarationAsync(Caller, financialYear, ct));
    }

    /// <summary>Saves declaration lines. Per-section save; the window is server-enforced.</summary>
    [HttpPut("tax/declarations")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SaveDeclaration(
        [FromBody] SaveTaxDeclarationRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await payroll.SaveDeclarationAsync(Caller, request, ct));

    [HttpPost("tax/declarations/submit")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SubmitDeclaration(
        [FromBody] SubmitTaxDeclarationRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await payroll.SubmitDeclarationAsync(Caller, request, ct));

    /// <summary>
    /// Per-line verification against the attached proof. A rejected line returns only that
    /// line for correction, never the whole declaration (WF-007.H2).
    /// </summary>
    [HttpPost("tax/declarations/{employeeId}/lines/verification")]
    [RequirePermission("PAYROLL.TAX_VERIFY")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> VerifyDeclarationLine(
        string employeeId,
        [FromBody] VerifyTaxDeclarationLineRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await payroll.VerifyDeclarationLineAsync(Caller, employeeId, request, ct));

    [HttpGet("salary-structure")]
    public async Task<ActionResult<ApiEnvelope<SalaryStructureDto>>> SalaryStructure(
        [FromQuery] string? employeeId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await payroll.GetSalaryStructureAsync(Caller, employeeId, ct));
    }
}

// MBK-029 — WF-006 -----------------------------------------------------------

[Route("api/mobile/v1/reimbursements")]
public sealed class ReimbursementsController(IReimbursementService reimbursements)
    : MobileApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<CursorPage<ReimbursementListItemDto>>>> List(
        [FromQuery] ReimbursementListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await reimbursements.ListAsync(Caller, request, ct));

    [HttpGet("{claimId}")]
    public async Task<ActionResult<ApiEnvelope<ReimbursementDetailDto>>> Get(
        string claimId,
        CancellationToken ct) =>
        OkEnvelope(await reimbursements.GetAsync(Caller, claimId, ct));

    [HttpPost("draft")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> CreateDraft(
        [FromBody] SaveReimbursementRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await reimbursements.SaveDraftAsync(Caller, null, request, ct));

    [HttpPut("{claimId}/draft")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SaveDraft(
        string claimId,
        [FromBody] SaveReimbursementRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await reimbursements.SaveDraftAsync(Caller, claimId, request, ct));

    /// <summary>Receipts upload before submit and are referenced by ticket (S5).</summary>
    [HttpPost("{claimId}/receipts/intent")]
    public async Task<ActionResult<ApiEnvelope<UploadIntentDto>>> ReceiptIntent(
        string claimId,
        [FromBody] ReimbursementReceiptRequest request,
        CancellationToken ct) =>
        OkEnvelope(await reimbursements.CreateReceiptIntentAsync(Caller, claimId, request, ct));

    [HttpPost("{claimId}/submit")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Submit(
        string claimId,
        [FromBody] SubmitReimbursementRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await reimbursements.SubmitAsync(Caller, claimId, request, ct));

    [HttpPost("{claimId}/withdraw")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Withdraw(
        string claimId,
        CancellationToken ct) =>
        MutationEnvelope(await reimbursements.WithdrawAsync(Caller, claimId, ct));

    /// <summary>
    /// The only workflow permitting partial approval; the result records both the claimed
    /// and the approved amount (WF-006 edge cases).
    /// </summary>
    [HttpPost("{claimId}/decision")]
    [RequirePermission("REIMBURSEMENTS.APPROVE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Decide(
        string claimId,
        [FromBody] ReimbursementDecisionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await reimbursements.DecideAsync(Caller, claimId, request, ct));
}

// MBK-030 — WF-008 -----------------------------------------------------------

[Route("api/mobile/v1/benefits")]
public sealed class BenefitsController(IBenefitService benefits) : MobileApiControllerBase
{
    [HttpGet("plans")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<BenefitPlanDto>>>> Plans(
        [FromQuery] BenefitPlansQuery request,
        CancellationToken ct) =>
        OkEnvelope(await benefits.ListPlansAsync(Caller, request, ct));

    [HttpPost("compare")]
    public async Task<ActionResult<ApiEnvelope<BenefitComparisonDto>>> Compare(
        [FromBody] BenefitComparisonRequest request,
        CancellationToken ct) =>
        OkEnvelope(await benefits.CompareAsync(Caller, request, ct));

    [HttpGet("coverage")]
    public async Task<ActionResult<ApiEnvelope<BenefitCoverageDto>>> Coverage(CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await benefits.GetCoverageAsync(Caller, ct));
    }

    /// <summary>
    /// Enrolment is window-bound and server-validated. Outside the window the caller must
    /// raise a qualifying-life-event request through the HR service catalog instead.
    /// </summary>
    [HttpPost("enrollment")]
    public async Task<ActionResult<ApiEnvelope<BenefitEnrollmentResultDto>>> Enrol(
        [FromBody] SubmitBenefitEnrollmentRequest request,
        CancellationToken ct) =>
        OkEnvelope(await benefits.EnrolAsync(Caller, request, ct));
}
