using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Recruitment.Controllers;

// MBK-011 -------------------------------------------------------------------

[Route("api/mobile/v1/recruitment/dashboard")]
public sealed class RecruitmentDashboardController(IRecruitmentDashboardService dashboard)
    : MobileApiControllerBase
{
    [HttpGet]
    [RequirePermission("RECRUITMENT.VIEW")]
    public async Task<ActionResult<ApiEnvelope<RecruitmentDashboardDto>>> Get(CancellationToken ct) =>
        OkEnvelope(await dashboard.GetAsync(Caller, ct));
}

// MBK-012 — WF-015 stage 1 ---------------------------------------------------

[Route("api/mobile/v1/recruitment/requisitions")]
public sealed class RequisitionsController(IRequisitionService requisitions) : MobileApiControllerBase
{
    [HttpGet]
    [RequirePermission("RECRUITMENT.VIEW")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<RequisitionListItemDto>>>> List(
        [FromQuery] RequisitionListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await requisitions.ListAsync(Caller, request, ct));

    [HttpGet("{requisitionId:long}")]
    [RequirePermission("RECRUITMENT.VIEW")]
    public async Task<ActionResult<ApiEnvelope<RequisitionDetailDto>>> Get(
        long requisitionId,
        CancellationToken ct) =>
        OkEnvelope(await requisitions.GetAsync(Caller, requisitionId, ct));

    [HttpPost("draft")]
    [RequirePermission("REQUISITIONS.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> CreateDraft(
        [FromBody] SaveRequisitionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await requisitions.SaveDraftAsync(Caller, null, request, ct));

    [HttpPut("{requisitionId:long}/draft")]
    [RequirePermission("REQUISITIONS.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SaveDraft(
        long requisitionId,
        [FromBody] SaveRequisitionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await requisitions.SaveDraftAsync(Caller, requisitionId, request, ct));

    /// <summary>Submits the requisition into its approval chain (WF-015.E2).</summary>
    [HttpPost("{requisitionId:long}/submit")]
    [RequirePermission("REQUISITIONS.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Submit(
        long requisitionId,
        CancellationToken ct) =>
        MutationEnvelope(await requisitions.SubmitAsync(Caller, requisitionId, ct));

    [HttpPost("{requisitionId:long}/cancel")]
    [RequirePermission("REQUISITIONS.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Cancel(
        long requisitionId,
        [FromBody] CancelRequisitionBody body,
        CancellationToken ct) =>
        MutationEnvelope(await requisitions.CancelAsync(
            Caller, requisitionId, body.ReasonCode, body.Comment, ct));

    [HttpGet("{requisitionId:long}/approvals")]
    [RequirePermission("RECRUITMENT.VIEW")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<WorkflowStepDto>>>> Approvals(
        long requisitionId,
        CancellationToken ct) =>
        OkEnvelope(await requisitions.GetApprovalTrailAsync(Caller, requisitionId, ct));
}

public sealed class CancelRequisitionBody
{
    public string ReasonCode { get; set; } = string.Empty;

    public string? Comment { get; set; }
}

// MBK-015 -------------------------------------------------------------------

[Route("api/mobile/v1/recruitment/candidates")]
public sealed class CandidatesController(ICandidateService candidates) : MobileApiControllerBase
{
    /// <summary>
    /// Candidate PII, shaped by field permissions on the server. Never cached on device.
    /// </summary>
    [HttpGet("{candidateId}")]
    [RequirePermission("RECRUITMENT.CANDIDATE_PII")]
    public async Task<ActionResult<ApiEnvelope<CandidateProfileDto>>> Get(
        string candidateId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await candidates.GetProfileAsync(Caller, candidateId, ct));
    }

    [HttpPost("{candidateId}/resume/ticket")]
    [RequirePermission("RECRUITMENT.CANDIDATE_DOCUMENTS")]
    public async Task<ActionResult<ApiEnvelope<ProtectedFileTicketDto>>> ResumeTicket(
        string candidateId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await candidates.GetResumeTicketAsync(Caller, candidateId, ct));
    }
}

// MBK-016 — WF-010 on the candidate side --------------------------------------

[Route("api/mobile/v1/recruitment/candidate-documents")]
public sealed class CandidateDocumentsController(ICandidateDocumentService documents)
    : MobileApiControllerBase
{
    [HttpGet]
    [RequirePermission("RECRUITMENT.CANDIDATE_DOCUMENTS")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<CandidateDocumentDto>>>> List(
        [FromQuery] string applicationId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await documents.ListAsync(Caller, applicationId, ct));
    }

    [HttpPost("upload-intent")]
    [RequirePermission("RECRUITMENT.CANDIDATE_DOCUMENTS")]
    public async Task<ActionResult<ApiEnvelope<UploadIntentDto>>> UploadIntent(
        [FromQuery] string applicationId,
        [FromBody] RequestDocumentRequest request,
        CancellationToken ct) =>
        OkEnvelope(await documents.CreateUploadIntentAsync(Caller, applicationId, request, ct));

    [HttpPost("{documentId}/ticket")]
    [RequirePermission("RECRUITMENT.CANDIDATE_DOCUMENTS")]
    public async Task<ActionResult<ApiEnvelope<ProtectedFileTicketDto>>> Ticket(
        string documentId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await documents.GetTicketAsync(Caller, documentId, ct));
    }

    /// <summary>Verify, reject or ask again. Rejection must name the defect (WF-010.H2).</summary>
    [HttpPost("{documentId}/verification")]
    [RequirePermission("RECRUITMENT.DOCUMENT_VERIFY")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Verify(
        string documentId,
        [FromBody] VerifyDocumentRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await documents.VerifyAsync(Caller, documentId, request, ct));

    [HttpPost("request")]
    [RequirePermission("RECRUITMENT.CANDIDATE_DOCUMENTS")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> RequestFromCandidate(
        [FromQuery] string applicationId,
        [FromBody] RequestDocumentRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await documents.RequestFromCandidateAsync(Caller, applicationId, request, ct));
}

// MBK-017 -------------------------------------------------------------------

[Route("api/mobile/v1/recruitment/interviews")]
public sealed class InterviewsController(IInterviewService interviews) : MobileApiControllerBase
{
    [HttpGet]
    [RequirePermission("RECRUITMENT.INTERVIEWS")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<InterviewListItemDto>>>> List(
        [FromQuery] InterviewListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await interviews.ListAsync(Caller, request, ct));

    [HttpGet("{interviewId}")]
    [RequirePermission("RECRUITMENT.INTERVIEWS")]
    public async Task<ActionResult<ApiEnvelope<InterviewDetailDto>>> Get(
        string interviewId,
        CancellationToken ct) =>
        OkEnvelope(await interviews.GetAsync(Caller, interviewId, ct));

    /// <summary>Panel availability for the slot picker (MOB-044).</summary>
    [HttpGet("availability")]
    [RequirePermission("RECRUITMENT.INTERVIEWS_MANAGE")]
    public async Task<ActionResult<ApiEnvelope<InterviewAvailabilityDto>>> Availability(
        [FromQuery] InterviewAvailabilityQuery query,
        CancellationToken ct) =>
        OkEnvelope(await interviews.GetAvailabilityAsync(Caller, query, ct));

    [HttpPost]
    [RequirePermission("RECRUITMENT.INTERVIEWS_MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Schedule(
        [FromBody] ScheduleInterviewRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await interviews.ScheduleAsync(Caller, request, ct));

    [HttpPut("{interviewId}")]
    [RequirePermission("RECRUITMENT.INTERVIEWS_MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Reschedule(
        string interviewId,
        [FromBody] RescheduleInterviewRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await interviews.RescheduleAsync(Caller, interviewId, request, ct));

    [HttpPost("{interviewId}/cancel")]
    [RequirePermission("RECRUITMENT.INTERVIEWS_MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Cancel(
        string interviewId,
        [FromBody] CancelInterviewRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await interviews.CancelAsync(Caller, interviewId, request, ct));

    /// <summary>
    /// Evaluation is scoped to the assigned interviewer; the service enforces that, not the UI.
    /// </summary>
    [HttpGet("{interviewId}/evaluation")]
    public async Task<ActionResult<ApiEnvelope<InterviewEvaluationDto>>> GetEvaluation(
        string interviewId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await interviews.GetEvaluationAsync(Caller, interviewId, ct));
    }

    [HttpPut("{interviewId}/evaluation")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SaveEvaluation(
        string interviewId,
        [FromBody] SaveInterviewEvaluationRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await interviews.SaveEvaluationAsync(Caller, interviewId, request, ct));
}

// MBK-018 -------------------------------------------------------------------

[Route("api/mobile/v1/recruitment/offers")]
public sealed class OffersController(IOfferService offers) : MobileApiControllerBase
{
    [HttpGet]
    [RequirePermission("OFFERS.VIEW")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<OfferListItemDto>>>> List(
        [FromQuery] OfferListQuery request,
        CancellationToken ct)
    {
        // Compensation is never cached on the device.
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await offers.ListAsync(Caller, request, ct));
    }

    [HttpGet("{offerId}")]
    [RequirePermission("OFFERS.VIEW")]
    public async Task<ActionResult<ApiEnvelope<OfferDetailDto>>> Get(
        string offerId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await offers.GetAsync(Caller, offerId, ct));
    }

    [HttpPost]
    [RequirePermission("OFFERS.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Generate(
        [FromBody] GenerateOfferRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await offers.GenerateAsync(Caller, request, ct));

    /// <summary>Offer approval is a separate permission from sending it (WF-015.H12).</summary>
    [HttpPost("{offerId}/decision")]
    [RequirePermission("OFFERS.APPROVE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Decide(
        string offerId,
        [FromBody] OfferDecisionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await offers.DecideAsync(Caller, offerId, request, ct));

    [HttpPost("{offerId}/send")]
    [RequirePermission("OFFERS.SEND")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Send(
        string offerId,
        [FromBody] SendOfferRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await offers.SendAsync(Caller, offerId, request, ct));

    [HttpPost("{offerId}/document/ticket")]
    [RequirePermission("OFFERS.VIEW")]
    public async Task<ActionResult<ApiEnvelope<ProtectedFileTicketDto>>> DocumentTicket(
        string offerId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await offers.GetDocumentTicketAsync(Caller, offerId, ct));
    }
}
