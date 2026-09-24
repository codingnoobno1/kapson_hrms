using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Employee.Controllers;

// MBK-019 -------------------------------------------------------------------

[Route("api/mobile/v1/employees")]
public sealed class EmployeesController(IMobileEmployeeService employees) : MobileApiControllerBase
{
    [HttpGet]
    [RequirePermission("EMPLOYEES.VIEW")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<EmployeeListItemDto>>>> List(
        [FromQuery] EmployeeListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await employees.ListAsync(Caller, request, ct));

    [HttpGet("{employeeId}")]
    public async Task<ActionResult<ApiEnvelope<EmployeeOverviewDto>>> Overview(
        string employeeId,
        CancellationToken ct) =>
        OkEnvelope(await employees.GetOverviewAsync(Caller, employeeId, ct));

    // Personal identity ------------------------------------------------------

    [HttpGet("{employeeId}/personal")]
    public async Task<ActionResult<ApiEnvelope<EmployeePersonalDto>>> Personal(
        string employeeId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await employees.GetPersonalAsync(Caller, employeeId, ct));
    }

    /// <summary>
    /// Self-editable fields save directly; fields configured as controlled raise a change
    /// request instead (WF-009). The service decides which, not the client.
    /// </summary>
    [HttpPut("{employeeId}/personal")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> UpdatePersonal(
        string employeeId,
        [FromBody] UpdateEmployeePersonalRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await employees.UpdatePersonalAsync(Caller, employeeId, request, ct));

    // Employment and organisation --------------------------------------------

    [HttpGet("{employeeId}/employment")]
    public async Task<ActionResult<ApiEnvelope<EmployeeEmploymentDto>>> Employment(
        string employeeId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await employees.GetEmploymentAsync(Caller, employeeId, ct));
    }

    [HttpPut("{employeeId}/employment")]
    [RequirePermission("EMPLOYEES.EMPLOYMENT_MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> UpdateEmployment(
        string employeeId,
        [FromBody] UpdateEmployeeEmploymentRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await employees.UpdateEmploymentAsync(Caller, employeeId, request, ct));

    [HttpGet("{employeeId}/organisation")]
    public async Task<ActionResult<ApiEnvelope<EmployeeOrganisationDto>>> Organisation(
        string employeeId,
        CancellationToken ct) =>
        OkEnvelope(await employees.GetOrganisationAsync(Caller, employeeId, ct));

    [HttpPut("{employeeId}/organisation")]
    [RequirePermission("EMPLOYEES.ORGANISATION_MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> UpdateOrganisation(
        string employeeId,
        [FromBody] UpdateEmployeeOrganisationRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await employees.UpdateOrganisationAsync(Caller, employeeId, request, ct));

    [HttpGet("{employeeId}/compensation")]
    public async Task<ActionResult<ApiEnvelope<CompensationSnapshotDto>>> Compensation(
        string employeeId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await employees.GetCompensationAsync(Caller, employeeId, ct));
    }

    // Contacts and dependants -------------------------------------------------

    [HttpGet("{employeeId}/contacts")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<EmployeeContactDto>>>> Contacts(
        string employeeId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await employees.ListContactsAsync(Caller, employeeId, ct));
    }

    [HttpPut("{employeeId}/contacts")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SaveContact(
        string employeeId,
        [FromBody] SaveEmployeeContactRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await employees.SaveContactAsync(Caller, employeeId, request, ct));

    [HttpDelete("{employeeId}/contacts/{contactId}")]
    public async Task<IActionResult> RemoveContact(
        string employeeId,
        string contactId,
        CancellationToken ct)
    {
        await employees.RemoveContactAsync(Caller, employeeId, contactId, ct);
        return NoContent();
    }

    [HttpGet("{employeeId}/dependants")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<EmployeeDependantDto>>>> Dependants(
        string employeeId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await employees.ListDependantsAsync(Caller, employeeId, ct));
    }

    [HttpGet("{employeeId}/timeline")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<EmployeeTimelineEntryDto>>>> Timeline(
        string employeeId,
        [FromQuery] EmployeeTimelineQuery request,
        CancellationToken ct) =>
        OkEnvelope(await employees.GetTimelineAsync(Caller, employeeId, request, ct));

    // Creation (MOB-059–062) -------------------------------------------------

    [HttpPost("duplicate-check")]
    [RequirePermission("EMPLOYEES.CREATE")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<EmployeeListItemDto>>>> CheckDuplicate(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken ct) =>
        OkEnvelope(await employees.CheckDuplicateAsync(Caller, request, ct));

    [HttpPost]
    [RequirePermission("EMPLOYEES.CREATE")]
    public async Task<ActionResult<ApiEnvelope<CreateEmployeeResultDto>>> Create(
        [FromBody] CreateEmployeeRequest request,
        CancellationToken ct) =>
        OkEnvelope(await employees.CreateAsync(Caller, request, ct));

    // Controlled-field change requests (WF-009) --------------------------------

    [HttpPost("{employeeId}/change-requests")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> CreateChangeRequest(
        string employeeId,
        [FromBody] CreateEmployeeChangeRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await employees.CreateChangeRequestAsync(Caller, employeeId, request, ct));

    [HttpGet("change-requests/{changeRequestId}")]
    public async Task<ActionResult<ApiEnvelope<EmployeeChangeRequestDto>>> GetChangeRequest(
        string changeRequestId,
        CancellationToken ct) =>
        OkEnvelope(await employees.GetChangeRequestAsync(Caller, changeRequestId, ct));
}

// MBK-020 — WF-010 -----------------------------------------------------------

[Route("api/mobile/v1/employees/{employeeId}/documents")]
public sealed class EmployeeDocumentsController(IEmployeeDocumentService documents)
    : MobileApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<EmployeeDocumentDto>>>> List(
        string employeeId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await documents.ListAsync(Caller, employeeId, ct));
    }

    [HttpPost("upload-intent")]
    public async Task<ActionResult<ApiEnvelope<UploadIntentDto>>> UploadIntent(
        string employeeId,
        [FromBody] DocumentUploadIntentRequest request,
        CancellationToken ct) =>
        OkEnvelope(await documents.CreateUploadIntentAsync(Caller, employeeId, request, ct));

    [HttpPost("confirm")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> ConfirmUpload(
        string employeeId,
        [FromBody] ConfirmDocumentUploadRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await documents.ConfirmUploadAsync(Caller, employeeId, request, ct));

    [HttpPost("{documentId}/ticket")]
    public async Task<ActionResult<ApiEnvelope<ProtectedFileTicketDto>>> Ticket(
        string employeeId,
        string documentId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await documents.GetTicketAsync(Caller, employeeId, documentId, ct));
    }

    [HttpPost("{documentId}/verification")]
    [RequirePermission("EMPLOYEES.DOCUMENT_VERIFY")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Verify(
        string employeeId,
        string documentId,
        [FromBody] VerifyEmployeeDocumentRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await documents.VerifyAsync(Caller, employeeId, documentId, request, ct));

    [HttpDelete("{documentId}")]
    public async Task<IActionResult> Remove(
        string employeeId,
        string documentId,
        CancellationToken ct)
    {
        await documents.RemoveAsync(Caller, employeeId, documentId, ct);
        return NoContent();
    }
}

// MBK-021 — WF-012. Complements the implemented HrOnboardingController. -------

[Route("api/mobile/v1/onboarding")]
public sealed class MobileOnboardingController(IMobileOnboardingService onboarding)
    : MobileApiControllerBase
{
    /// <summary>Cohort view: progress, overdue tasks and owners (MOB-063).</summary>
    [HttpGet]
    [RequirePermission("ONBOARDING.VIEW")]
    public async Task<ActionResult<ApiEnvelope<OnboardingDashboardDto>>> Dashboard(
        [FromQuery] OnboardingQuery request,
        CancellationToken ct) =>
        OkEnvelope(await onboarding.GetDashboardAsync(Caller, request, ct));

    [HttpGet("{employeeId}/checklist")]
    public async Task<ActionResult<ApiEnvelope<OnboardingSummaryDto>>> Checklist(
        string employeeId,
        CancellationToken ct) =>
        OkEnvelope(await onboarding.GetChecklistAsync(Caller, employeeId, ct));

    [HttpGet("tasks/{taskId}")]
    public async Task<ActionResult<ApiEnvelope<OnboardingTaskDetailDto>>> Task(
        string taskId,
        CancellationToken ct) =>
        OkEnvelope(await onboarding.GetTaskAsync(Caller, taskId, ct));

    [HttpPost("tasks/{taskId}/complete")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> CompleteTask(
        string taskId,
        [FromBody] CompleteOnboardingTaskRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await onboarding.CompleteTaskAsync(Caller, taskId, request, ct));

    [HttpPost("tasks/{taskId}/reassign")]
    [RequirePermission("ONBOARDING.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> ReassignTask(
        string taskId,
        [FromBody] ReassignOnboardingTaskRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await onboarding.ReassignTaskAsync(Caller, taskId, request, ct));

    [HttpPost("tasks/{taskId}/comments")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> CommentTask(
        string taskId,
        [FromBody] AddTaskCommentRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await onboarding.CommentTaskAsync(Caller, taskId, request, ct));

    /// <summary>Documents awaiting verification for one joiner (MOB-066).</summary>
    [HttpGet("{employeeId}/verification")]
    [RequirePermission("EMPLOYEES.DOCUMENT_VERIFY")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<EmployeeDocumentDto>>>> Verification(
        string employeeId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await onboarding.GetVerificationQueueAsync(Caller, employeeId, ct));
    }
}

// MBK-022 — WF-013 -----------------------------------------------------------

[Route("api/mobile/v1/probation")]
public sealed class ProbationController(IProbationService probation) : MobileApiControllerBase
{
    [HttpGet("{employeeId}")]
    [RequirePermission("PROBATION.VIEW")]
    public async Task<ActionResult<ApiEnvelope<ProbationDetailDto>>> Get(
        string employeeId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await probation.GetAsync(Caller, employeeId, ct));
    }

    [HttpPut("{employeeId}/review")]
    [RequirePermission("PROBATION.REVIEW")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SaveReview(
        string employeeId,
        [FromBody] SaveProbationReviewRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await probation.SaveReviewAsync(Caller, employeeId, request, ct));

    /// <summary>Confirm, extend or recommend separation (WF-013.H2).</summary>
    [HttpPost("{employeeId}/decision")]
    [RequirePermission("PROBATION.DECIDE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Decide(
        string employeeId,
        [FromBody] ProbationDecisionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await probation.DecideAsync(Caller, employeeId, request, ct));
}

// MBK-023 — WF-014 -----------------------------------------------------------

[Route("api/mobile/v1/offboarding")]
public sealed class OffboardingController(IOffboardingService offboarding) : MobileApiControllerBase
{
    [HttpGet("{employeeId}")]
    [RequirePermission("OFFBOARDING.VIEW")]
    public async Task<ActionResult<ApiEnvelope<OffboardingDetailDto>>> Get(
        string employeeId,
        CancellationToken ct)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await offboarding.GetAsync(Caller, employeeId, ct));
    }

    [HttpPost("{employeeId}")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Initiate(
        string employeeId,
        [FromBody] InitiateOffboardingRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await offboarding.InitiateAsync(Caller, employeeId, request, ct));

    [HttpPut("{employeeId}")]
    [RequirePermission("OFFBOARDING.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Update(
        string employeeId,
        [FromBody] UpdateOffboardingRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await offboarding.UpdateAsync(Caller, employeeId, request, ct));

    /// <summary>Clearance fans out to owners; each owner sees only their own tasks.</summary>
    [HttpGet("{employeeId}/clearance")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<ClearanceTaskDto>>>> Clearance(
        string employeeId,
        CancellationToken ct) =>
        OkEnvelope(await offboarding.GetClearanceAsync(Caller, employeeId, ct));

    [HttpPost("{employeeId}/clearance/{taskId}")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> UpdateClearanceTask(
        string employeeId,
        string taskId,
        [FromBody] UpdateClearanceTaskRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await offboarding.UpdateClearanceTaskAsync(
            Caller, employeeId, taskId, request, ct));

    [HttpPost("{employeeId}/close")]
    [RequirePermission("OFFBOARDING.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Close(
        string employeeId,
        [FromBody] CloseOffboardingRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await offboarding.CloseAsync(Caller, employeeId, request, ct));
}
