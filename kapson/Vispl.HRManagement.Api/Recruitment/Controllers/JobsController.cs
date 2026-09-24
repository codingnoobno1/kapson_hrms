using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Recruitment;

namespace Vispl.HRManagement.Api.Recruitment.Controllers;

[Route("api/mobile/v1/recruitment/jobs")]
public sealed class JobsController(
    IJobOpeningService jobOpeningService,
    IMobileJobService mobileJobs)
    : MobileApiControllerBase
{
    [HttpGet]
    [RequirePermission("RECRUITMENT.VIEW")]
    [ProducesResponseType<ApiEnvelope<CursorPage<JobListItemDto>>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiEnvelope<CursorPage<JobListItemDto>>>> GetAsync(
        [FromQuery] JobListQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<CursorPage<JobListItemDto>>();
        }

        var openings = await jobOpeningService.GetAllAsync(TenantKey, cancellationToken);
        IEnumerable<JobOpeningDto> filtered = openings;

        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            filtered = filtered.Where(item =>
                item.Title.Contains(request.Query, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(request.Query, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Department))
        {
            filtered = filtered.Where(item =>
                string.Equals(item.Department, request.Department, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Location))
        {
            filtered = filtered.Where(item =>
                string.Equals(item.Location, request.Location, StringComparison.OrdinalIgnoreCase));
        }

        filtered = request.Status?.ToLowerInvariant() switch
        {
            "published" or "active" => filtered.Where(item => item.IsPublished),
            "draft" or "inactive" => filtered.Where(item => !item.IsPublished),
            _ => filtered
        };

        var materialized = filtered
            .OrderByDescending(item => item.UpdatedUtc)
            .ThenBy(item => item.Title)
            .ToList();
        var offset = CursorCodec.DecodeOffset(request.Cursor);
        var pageItems = materialized.Skip(offset).Take(request.PageSize).Select(MapListItem).ToList();
        var nextOffset = offset + pageItems.Count;
        var hasMore = nextOffset < materialized.Count;

        return OkEnvelope(new CursorPage<JobListItemDto>(
            pageItems,
            hasMore ? CursorCodec.EncodeOffset(nextOffset) : null,
            hasMore,
            request.PageSize,
            materialized.Count));
    }

    [HttpGet("{id:long}")]
    [RequirePermission("RECRUITMENT.VIEW")]
    [ProducesResponseType<ApiEnvelope<JobDetailDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiEnvelope<JobDetailDto>>> GetByIdAsync(
        long id,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<JobDetailDto>();
        }

        var opening = await jobOpeningService.GetByIdAsync(id, cancellationToken);
        if (opening is null || !string.Equals(opening.TenantKey, TenantKey, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var permissions = User.FindAll("permission").Select(claim => claim.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var canManage = permissions.Contains("RECRUITMENT.MANAGE");
        var canPublish = permissions.Contains("RECRUITMENT.PUBLISH");
        var actions = new List<ActionAvailabilityDto>
        {
            new("edit", canManage, canManage ? null : "Permission required."),
            new(opening.IsPublished ? "withdraw" : "publish", canPublish, canPublish ? null : "Permission required.")
        };

        var result = new JobDetailDto(
            opening.Id,
            opening.Slug,
            opening.Title,
            opening.Department,
            opening.Location,
            opening.EmploymentType,
            opening.WorkMode,
            opening.Experience,
            opening.Summary,
            opening.Responsibilities,
            opening.Requirements,
            opening.IsPublished,
            opening.PublishedUtc,
            opening.UpdatedUtc,
            actions,
            opening.Status,
            opening.ExpiresOn,
            FormatSalary(opening),
            DateOnly.FromDateTime((opening.StatusChangedUtc ?? opening.UpdatedUtc).LocalDateTime));

        Response.Headers.CacheControl = "private, no-cache";
        return OkEnvelope(result);
    }

    // Authoring, publication and analytics (MOB-029–034). Reads above are served from the
    // implemented job-opening service; these operations belong to the mobile job workflow.

    [HttpPost("draft")]
    [RequirePermission("RECRUITMENT.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> CreateDraftAsync(
        [FromBody] SaveJobRequest request,
        CancellationToken cancellationToken) =>
        MutationEnvelope(await mobileJobs.SaveDraftAsync(Caller, null, request, cancellationToken));

    [HttpPut("{id:long}/draft")]
    [RequirePermission("RECRUITMENT.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SaveDraftAsync(
        long id,
        [FromBody] SaveJobRequest request,
        CancellationToken cancellationToken) =>
        MutationEnvelope(await mobileJobs.SaveDraftAsync(Caller, id, request, cancellationToken));

    [HttpPut("{id:long}/compensation")]
    [RequirePermission("RECRUITMENT.COMPENSATION")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SaveCompensationAsync(
        long id,
        [FromBody] SaveJobCompensationRequest request,
        CancellationToken cancellationToken) =>
        MutationEnvelope(await mobileJobs.SaveCompensationAsync(Caller, id, request, cancellationToken));

    [HttpGet("{id:long}/validate")]
    [RequirePermission("RECRUITMENT.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<ApiWarningDto>>>> ValidateAsync(
        long id,
        CancellationToken cancellationToken) =>
        OkEnvelope(await mobileJobs.ValidateAsync(Caller, id, cancellationToken));

    /// <summary>
    /// Publishes or schedules publication. Blocked when the job has no approved requisition
    /// with remaining headcount (WF-015 stage 2).
    /// </summary>
    [HttpPost("{id:long}/publish")]
    [RequirePermission("RECRUITMENT.PUBLISH")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> PublishAsync(
        long id,
        [FromBody] PublishJobRequest request,
        CancellationToken cancellationToken) =>
        MutationEnvelope(await mobileJobs.PublishAsync(Caller, id, request, cancellationToken));

    [HttpPost("{id:long}/status")]
    [RequirePermission("RECRUITMENT.PUBLISH")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> ChangeStatusAsync(
        long id,
        [FromBody] ChangeJobStatusRequest request,
        CancellationToken cancellationToken) =>
        MutationEnvelope(await mobileJobs.ChangeStatusAsync(Caller, id, request, cancellationToken));

    [HttpPost("{id:long}/duplicate")]
    [RequirePermission("RECRUITMENT.MANAGE")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> DuplicateAsync(
        long id,
        CancellationToken cancellationToken) =>
        MutationEnvelope(await mobileJobs.DuplicateAsync(Caller, id, cancellationToken));

    [HttpGet("{id:long}/analytics")]
    [RequirePermission("RECRUITMENT.ANALYTICS")]
    public async Task<ActionResult<ApiEnvelope<JobAnalyticsDto>>> AnalyticsAsync(
        long id,
        [FromQuery] JobAnalyticsQuery query,
        CancellationToken cancellationToken) =>
        OkEnvelope(await mobileJobs.GetAnalyticsAsync(Caller, id, query, cancellationToken));

    private static JobListItemDto MapListItem(JobOpeningDto opening) => new(
        opening.Id,
        opening.Slug,
        opening.Title,
        opening.Department,
        opening.Location,
        opening.EmploymentType,
        opening.WorkMode,
        opening.Experience,
        opening.Summary,
        opening.IsPublished,
        opening.PublishedUtc,
        opening.UpdatedUtc,
        opening.Status,
        opening.ExpiresOn,
        FormatSalary(opening),
        DateOnly.FromDateTime((opening.StatusChangedUtc ?? opening.UpdatedUtc).LocalDateTime));

    /// <summary>
    /// Indian digit grouping, as the reference card shows. Returns null rather than a partial
    /// range when either bound is missing, so the client renders its own placeholder.
    /// </summary>
    private static string? FormatSalary(JobOpeningDto opening)
    {
        if (opening.SalaryMinimum is not { } minimum || opening.SalaryMaximum is not { } maximum)
        {
            return null;
        }

        var culture = CultureInfo.GetCultureInfo("en-IN");
        var symbol = string.IsNullOrWhiteSpace(opening.SalaryCurrencyCode) ||
                     opening.SalaryCurrencyCode == "INR"
            ? "₹"
            : opening.SalaryCurrencyCode + " ";

        return $"{symbol}{minimum.ToString("#,##0", culture)} - {symbol}{maximum.ToString("#,##0", culture)} p.a.";
    }
}
