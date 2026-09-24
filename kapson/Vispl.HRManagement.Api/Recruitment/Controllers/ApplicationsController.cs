using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Recruitment;
using MobileRecruitment = Vispl.HRManagement.Contracts.Mobile.Recruitment;

namespace Vispl.HRManagement.Api.Recruitment.Controllers;

[Route("api/mobile/v1/recruitment/applications")]
public sealed class ApplicationsController(
    IApplicationService applications,
    IMobileApplicationService mobileApplications) : MobileApiControllerBase
{
    [HttpGet]
    [RequirePermission("RECRUITMENT.APPLICATIONS")]
    [ProducesResponseType<ApiEnvelope<CursorPage<ApplicationListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<CursorPage<ApplicationListItemDto>>>> GetAsync(
        [FromQuery] string? stage,
        [FromQuery] string? query,
        [FromQuery] string? cursor,
        [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<CursorPage<ApplicationListItemDto>>();
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        var all = await applications.GetSummariesAsync(TenantKey, stage, query, cancellationToken);

        var offset = CursorCodec.DecodeOffset(cursor);
        var page = all.Skip(offset).Take(pageSize).ToList();
        var nextOffset = offset + page.Count;
        var hasMore = nextOffset < all.Count;

        return OkEnvelope(new CursorPage<ApplicationListItemDto>(
            page,
            hasMore ? CursorCodec.EncodeOffset(nextOffset) : null,
            hasMore,
            pageSize,
            all.Count));
    }

    [HttpGet("{id:long}")]
    [RequirePermission("RECRUITMENT.APPLICATIONS")]
    [ProducesResponseType<ApiEnvelope<ApplicationDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiEnvelope<ApplicationDetailDto>>> GetByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<ApplicationDetailDto>();
        }

        var detail = await applications.GetDetailAsync(TenantKey, id, cancellationToken);
        if (detail is null)
        {
            return NotFound();
        }

        // Candidate personal data: never cached on the device.
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(detail);
    }

    [HttpPost("{id:long}/stage")]
    [RequirePermission("RECRUITMENT.APPLICATIONS")]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> ChangeStageAsync(
        long id,
        [FromBody] ChangeStageRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<MutationResultDto>();
        }

        var actorId = User.FindFirst("sub")?.Value;
        var canManage = User.FindAll("permission")
            .Any(claim => string.Equals(claim.Value, "RECRUITMENT.MANAGE", StringComparison.OrdinalIgnoreCase));

        try
        {
            await applications.ChangeStageAsync(TenantKey, id, request, actorId, canManage, cancellationToken);
        }
        catch (ApplicationRejectedException rejected)
        {
            var problem = new ProblemDetails
            {
                Title = rejected.Message,
                Status = StatusCodes.Status400BadRequest,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = rejected.Code;
            problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;
            return BadRequest(problem);
        }

        return OkEnvelope(new MutationResultDto(
            id.ToString(), request.ToStage, null, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Streams a candidate document after a permission check. Files are never reachable by
    /// a static path, and a file that has not been scan-cleared is not released.
    /// </summary>
    [HttpGet("{id:long}/documents/{documentId:long}")]
    [RequirePermission("RECRUITMENT.APPLICATIONS")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAsync(
        long id,
        long documentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return Forbid();
        }

        // Confirms the document belongs to an application this tenant can see.
        var detail = await applications.GetDetailAsync(TenantKey, id, cancellationToken);
        if (detail is null || detail.Documents.All(document => document.Id != documentId))
        {
            return NotFound();
        }

        try
        {
            var stream = await applications.OpenDocumentAsync(TenantKey, documentId, cancellationToken);
            if (stream is null)
            {
                return NotFound();
            }

            Response.Headers.CacheControl = "no-store";
            Response.Headers.ContentDisposition = "attachment";
            return File(stream.Content, stream.ContentType, stream.FileName);
        }
        catch (ApplicationRejectedException rejected)
        {
            var problem = new ProblemDetails
            {
                Title = rejected.Message,
                Status = StatusCodes.Status409Conflict,
                Instance = HttpContext.Request.Path
            };
            problem.Extensions["code"] = rejected.Code;
            return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };
        }
    }

    // Pipeline, screening and decisions (MOB-034, 036, 040–042).

    /// <summary>Stage columns and counts for one job (MOB-034).</summary>
    [HttpGet("pipeline")]
    [RequirePermission("RECRUITMENT.APPLICATIONS")]
    public async Task<ActionResult<ApiEnvelope<MobileRecruitment.ApplicantPipelineDto>>> PipelineAsync(
        [FromQuery] long jobId,
        CancellationToken cancellationToken) =>
        OkEnvelope(await mobileApplications.GetPipelineAsync(Caller, jobId, cancellationToken));

    /// <summary>
    /// Shortlist or reject with a reason code and optional templated candidate notification
    /// (MOB-042). Live only.
    /// </summary>
    [HttpPost("{id:long}/decision")]
    [RequirePermission("RECRUITMENT.DECISION")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> DecideAsync(
        long id,
        [FromBody] MobileRecruitment.ApplicationDecisionRequest request,
        CancellationToken cancellationToken) =>
        MutationEnvelope(await mobileApplications.DecideAsync(
            Caller, id.ToString(), request, cancellationToken));

    [HttpPost("{id:long}/rating")]
    [RequirePermission("RECRUITMENT.APPLICATIONS")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> RateAsync(
        long id,
        [FromBody] MobileRecruitment.RateApplicationRequest request,
        CancellationToken cancellationToken) =>
        MutationEnvelope(await mobileApplications.RateAsync(
            Caller, id.ToString(), request, cancellationToken));

    [HttpGet("{id:long}/screening")]
    [RequirePermission("RECRUITMENT.SCREENING")]
    public async Task<ActionResult<ApiEnvelope<MobileRecruitment.ScreeningScorecardDto>>> GetScreeningAsync(
        long id,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await mobileApplications.GetScreeningAsync(
            Caller, id.ToString(), cancellationToken));
    }

    /// <summary>Saves a screening draft, or submits it when <c>Submit</c> is set (MOB-040).</summary>
    [HttpPut("{id:long}/screening")]
    [RequirePermission("RECRUITMENT.SCREENING")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SaveScreeningAsync(
        long id,
        [FromBody] MobileRecruitment.SaveScreeningRequest request,
        CancellationToken cancellationToken) =>
        MutationEnvelope(await mobileApplications.SaveScreeningAsync(
            Caller, id.ToString(), request, cancellationToken));
}
