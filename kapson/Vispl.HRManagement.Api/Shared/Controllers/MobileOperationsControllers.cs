using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Operations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Shared.Controllers;

// MBK-031 — the single decision surface for every workflow --------------------

[Route("api/mobile/v1/approvals")]
public sealed class ApprovalsController(IApprovalService approvals) : MobileApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<CursorPage<ApprovalListItemDto>>>> Inbox(
        [FromQuery] ApprovalInboxQuery request,
        CancellationToken ct) =>
        OkEnvelope(await approvals.GetInboxAsync(Caller, request, ct));

    [HttpGet("{approvalId}")]
    public async Task<ActionResult<ApiEnvelope<ApprovalDetailDto>>> Get(
        string approvalId,
        CancellationToken ct) =>
        OkEnvelope(await approvals.GetAsync(Caller, approvalId, ct));

    /// <summary>
    /// Approve, reject or return. Live-only, version-checked and idempotent: a duplicate
    /// submit returns the original decision rather than recording a second one.
    /// </summary>
    [HttpPost("{approvalId}/decision")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Decide(
        string approvalId,
        [FromBody] ApprovalDecisionRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await approvals.DecideAsync(Caller, approvalId, request, ct));

    [HttpGet("delegations")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<ApprovalDelegationDto>>>> Delegations(
        CancellationToken ct) =>
        OkEnvelope(await approvals.ListDelegationsAsync(Caller, ct));

    [HttpPost("delegations")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SaveDelegation(
        [FromBody] SaveApprovalDelegationRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await approvals.SaveDelegationAsync(Caller, request, ct));

    [HttpDelete("delegations/{delegationId}")]
    public async Task<IActionResult> RevokeDelegation(string delegationId, CancellationToken ct)
    {
        await approvals.RevokeDelegationAsync(Caller, delegationId, ct);
        return NoContent();
    }
}

// MBK-032 -------------------------------------------------------------------

[Route("api/mobile/v1/requests")]
public sealed class HrRequestsController(IHrRequestService requests) : MobileApiControllerBase
{
    [HttpGet("catalog")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<HrServiceDefinitionDto>>>> Catalog(
        [FromQuery] RequestCatalogQuery request,
        CancellationToken ct) =>
        OkEnvelope(await requests.GetCatalogAsync(Caller, request, ct));

    [HttpGet("catalog/{serviceCode}/schema")]
    public async Task<ActionResult<ApiEnvelope<DynamicFormSchemaDto>>> Schema(
        string serviceCode,
        CancellationToken ct) =>
        OkEnvelope(await requests.GetSchemaAsync(Caller, serviceCode, ct));

    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<CursorPage<HrRequestDetailDto>>>> List(
        [FromQuery] HrRequestListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await requests.ListAsync(Caller, request, ct));

    [HttpPost]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Create(
        [FromBody] CreateHrRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await requests.CreateAsync(Caller, request, ct));

    [HttpGet("{requestId}")]
    public async Task<ActionResult<ApiEnvelope<HrRequestDetailDto>>> Get(
        string requestId,
        CancellationToken ct) =>
        OkEnvelope(await requests.GetAsync(Caller, requestId, ct));

    [HttpPost("{requestId}/comments")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Comment(
        string requestId,
        [FromBody] AddRequestCommentRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await requests.CommentAsync(Caller, requestId, request, ct));

    [HttpPost("{requestId}/cancel")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Cancel(
        string requestId,
        [FromBody] CancelHrRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await requests.CancelAsync(Caller, requestId, request, ct));

    [HttpPost("{requestId}/fulfil")]
    [RequirePermission("REQUESTS.FULFIL")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Fulfil(
        string requestId,
        [FromBody] FulfilHrRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await requests.FulfilAsync(Caller, requestId, request, ct));
}

// MBK-033 -------------------------------------------------------------------

[Route("api/mobile/v1/reports")]
public sealed class ReportsController(IReportService reports) : MobileApiControllerBase
{
    [HttpGet("catalog")]
    [RequirePermission("REPORTS.VIEW")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<ReportDefinitionDto>>>> Catalog(
        [FromQuery] ReportCatalogQuery request,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetCatalogAsync(Caller, request, ct));

    [HttpGet("{reportCode}/schema")]
    [RequirePermission("REPORTS.VIEW")]
    public async Task<ActionResult<ApiEnvelope<ReportFilterSchemaDto>>> Schema(
        string reportCode,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetSchemaAsync(Caller, reportCode, ct));

    [HttpPost("{reportCode}/run")]
    [RequirePermission("REPORTS.VIEW")]
    public async Task<ActionResult<ApiEnvelope<ReportResultDto>>> Run(
        string reportCode,
        [FromBody] RunReportRequest request,
        CancellationToken ct)
    {
        request.ReportCode = reportCode;
        return OkEnvelope(await reports.RunAsync(Caller, request, ct));
    }

    [HttpGet("{reportCode}/page")]
    [RequirePermission("REPORTS.VIEW")]
    public async Task<ActionResult<ApiEnvelope<ReportResultDto>>> Page(
        string reportCode,
        [FromQuery] ReportPageQuery request,
        CancellationToken ct) =>
        OkEnvelope(await reports.GetPageAsync(Caller, reportCode, request, ct));

    [HttpPost("{reportCode}/drilldown")]
    [RequirePermission("REPORTS.VIEW")]
    public async Task<ActionResult<ApiEnvelope<ReportDrilldownDto>>> DrillDown(
        string reportCode,
        [FromBody] ReportDrilldownRequest request,
        CancellationToken ct) =>
        OkEnvelope(await reports.DrillDownAsync(Caller, reportCode, request, ct));
}

// MBK-034 -------------------------------------------------------------------

[Route("api/mobile/v1/exports")]
public sealed class ExportsController(IExportService exports) : MobileApiControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ApiEnvelope<ExportJobDto>>> Create(
        [FromBody] CreateExportRequest request,
        CancellationToken ct) =>
        OkEnvelope(await exports.CreateAsync(Caller, request, ct));

    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<CursorPage<ExportJobDto>>>> List(
        [FromQuery] ExportListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await exports.ListAsync(Caller, request, ct));

    [HttpGet("{exportId}")]
    public async Task<ActionResult<ApiEnvelope<ExportJobDto>>> Get(
        string exportId,
        CancellationToken ct) =>
        OkEnvelope(await exports.GetAsync(Caller, exportId, ct));

    [HttpPost("{exportId}/ticket")]
    public async Task<ActionResult<ApiEnvelope<ProtectedFileTicketDto>>> Ticket(
        string exportId,
        CancellationToken ct) =>
        OkEnvelope(await exports.GetTicketAsync(Caller, exportId, ct));

    [HttpDelete("{exportId}")]
    public async Task<IActionResult> Delete(string exportId, CancellationToken ct)
    {
        await exports.DeleteAsync(Caller, exportId, ct);
        return NoContent();
    }
}

// MBK-035 -------------------------------------------------------------------

[Route("api/mobile/v1/profile")]
public sealed class ProfileController(IMobileProfileService profile) : MobileApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<OwnProfileDto>>> Get(CancellationToken ct) =>
        OkEnvelope(await profile.GetAsync(Caller, ct));

    [HttpPut]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Update(
        [FromBody] UpdateOwnProfileRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await profile.UpdateAsync(Caller, request, ct));

    [HttpGet("preferences")]
    public async Task<ActionResult<ApiEnvelope<AppPreferencesDto>>> Preferences(CancellationToken ct) =>
        OkEnvelope(await profile.GetPreferencesAsync(Caller, ct));

    [HttpPut("preferences")]
    public async Task<ActionResult<ApiEnvelope<AppPreferencesDto>>> UpdatePreferences(
        [FromBody] UpdateAppPreferencesRequest request,
        CancellationToken ct) =>
        OkEnvelope(await profile.UpdatePreferencesAsync(Caller, request, ct));

    [HttpGet("notifications")]
    public async Task<ActionResult<ApiEnvelope<NotificationPreferencesDto>>> NotificationPreferences(
        CancellationToken ct) =>
        OkEnvelope(await profile.GetNotificationPreferencesAsync(Caller, ct));

    [HttpPut("notifications")]
    public async Task<ActionResult<ApiEnvelope<NotificationPreferencesDto>>> UpdateNotificationPreferences(
        [FromBody] UpdateNotificationPreferencesRequest request,
        CancellationToken ct) =>
        OkEnvelope(await profile.UpdateNotificationPreferencesAsync(Caller, request, ct));

    [HttpPost("avatar/intent")]
    public async Task<ActionResult<ApiEnvelope<UploadIntentDto>>> AvatarIntent(
        [FromBody] AvatarUploadIntentRequest request,
        CancellationToken ct) =>
        OkEnvelope(await profile.CreateAvatarIntentAsync(Caller, request, ct));
}

// MBK-036 -------------------------------------------------------------------

[Route("api/mobile/v1/support")]
public sealed class SupportController(ISupportService support) : MobileApiControllerBase
{
    [HttpGet("categories")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<MasterReferenceDto>>>> Categories(
        CancellationToken ct) =>
        OkEnvelope(await support.ListCategoriesAsync(Caller, ct));

    [HttpGet("articles")]
    public async Task<ActionResult<ApiEnvelope<CursorPage<HelpArticleSummaryDto>>>> Articles(
        [FromQuery] HelpSearchQuery request,
        CancellationToken ct) =>
        OkEnvelope(await support.SearchArticlesAsync(Caller, request, ct));

    [HttpGet("articles/{articleCode}")]
    public async Task<ActionResult<ApiEnvelope<HelpArticleDto>>> Article(
        string articleCode,
        CancellationToken ct) =>
        OkEnvelope(await support.GetArticleAsync(Caller, articleCode, ct));

    [HttpPost("tickets")]
    public async Task<ActionResult<ApiEnvelope<SupportTicketResultDto>>> CreateTicket(
        [FromBody] CreateSupportTicketRequest request,
        CancellationToken ct) =>
        OkEnvelope(await support.CreateTicketAsync(Caller, request, ct));

    [HttpPost("diagnostics")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Diagnostics(
        [FromBody] SubmitDiagnosticsRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await support.SubmitDiagnosticsAsync(Caller, request, ct));
}

// MBK-037 -------------------------------------------------------------------

[Route("api/mobile/v1/sync")]
public sealed class SyncController(ISyncService sync) : MobileApiControllerBase
{
    [HttpGet("manifest")]
    public async Task<ActionResult<ApiEnvelope<SyncManifestDto>>> Manifest(CancellationToken ct) =>
        OkEnvelope(await sync.GetManifestAsync(Caller, ct));

    [HttpGet("changes")]
    public async Task<ActionResult<ApiEnvelope<SyncChangePageDto>>> Changes(
        [FromQuery] SyncChangesQuery query,
        CancellationToken ct) =>
        OkEnvelope(await sync.GetChangesAsync(Caller, query, ct));

    /// <summary>
    /// Resolves queued offline submissions by idempotency key so the client can tell a
    /// replayed mutation from a new one after reconnecting.
    /// </summary>
    [HttpPost("mutations/status")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<SyncMutationStatusDto>>>> MutationStatus(
        [FromBody] SyncMutationStatusQuery query,
        CancellationToken ct) =>
        OkEnvelope(await sync.GetMutationStatusAsync(Caller, query, ct));
}

// MBK-038 -------------------------------------------------------------------

[Route("api/mobile/v1/files")]
public sealed class ProtectedFilesController(IProtectedFileService files) : MobileApiControllerBase
{
    [HttpPost("tickets")]
    public async Task<ActionResult<ApiEnvelope<ProtectedFileTicketDto>>> CreateTicket(
        [FromBody] FileTicketRequest request,
        CancellationToken ct) =>
        OkEnvelope(await files.CreateTicketAsync(Caller, request, ct));

    [HttpGet("{fileId}/metadata")]
    public async Task<ActionResult<ApiEnvelope<FileMetadataDto>>> Metadata(
        string fileId,
        CancellationToken ct) =>
        OkEnvelope(await files.GetMetadataAsync(Caller, fileId, ct));

    [HttpDelete("{fileId}")]
    public async Task<IActionResult> Delete(string fileId, CancellationToken ct)
    {
        await files.DeleteAsync(Caller, fileId, ct);
        return NoContent();
    }
}
