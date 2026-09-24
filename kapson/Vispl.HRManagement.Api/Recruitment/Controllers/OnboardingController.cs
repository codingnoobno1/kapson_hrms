using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Recruitment;

namespace Vispl.HRManagement.Api.Recruitment.Controllers;

public sealed class HrTaskCompletionRequest
{
    public Dictionary<string, string> Values { get; set; } = [];
}

/// <summary>HR-facing onboarding review. Candidate-facing routes live on CandidateOnboardingController.</summary>
[Route("api/mobile/v1/onboarding")]
public sealed class HrOnboardingController(IOnboardingService onboarding) : MobileApiControllerBase
{
    [HttpGet("{id:long}")]
    [RequirePermission("EMPLOYEES.VIEW")]
    [ProducesResponseType<ApiEnvelope<OnboardingDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiEnvelope<OnboardingDto>>> GetAsync(long id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<OnboardingDto>();
        }

        var result = await onboarding.GetAsync(TenantKey, id, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(result);
    }

    [HttpGet("by-application/{applicationId:long}")]
    [RequirePermission("EMPLOYEES.VIEW")]
    public async Task<ActionResult<ApiEnvelope<OnboardingDto>>> GetByApplicationAsync(
        long applicationId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<OnboardingDto>();
        }

        var result = await onboarding.GetByApplicationAsync(TenantKey, applicationId, cancellationToken);
        return result is null ? NotFound() : OkEnvelope(result);
    }

    /// <summary>Records the outcome of an HR-owned task, such as background verification.</summary>
    [HttpPost("{id:long}/tasks/{taskId:long}/complete")]
    [RequirePermission("EMPLOYEES.CREATE")]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> CompleteAsync(
        long id,
        long taskId,
        [FromBody] HrTaskCompletionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<MutationResultDto>();
        }

        try
        {
            await onboarding.CompleteHrTaskAsync(
                TenantKey, id, taskId, request.Values, User.FindFirst("sub")?.Value, cancellationToken);
        }
        catch (ApplicationRejectedException rejected)
        {
            return BadRequest(RecruitmentProblems.From(HttpContext, rejected));
        }

        return OkEnvelope(new MutationResultDto(
            taskId.ToString(), OnboardingTaskStatuses.Approved, null, DateTimeOffset.UtcNow));
    }

    [HttpPost("{id:long}/tasks/{taskId:long}/decision")]
    [RequirePermission("EMPLOYEES.CREATE")]
    [ProducesResponseType<ApiEnvelope<MutationResultDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> DecideAsync(
        long id,
        long taskId,
        [FromBody] OnboardingTaskDecisionRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<MutationResultDto>();
        }

        try
        {
            await onboarding.DecideTaskAsync(TenantKey, taskId, request, User.FindFirst("sub")?.Value, cancellationToken);
        }
        catch (ApplicationRejectedException rejected)
        {
            return BadRequest(RecruitmentProblems.From(HttpContext, rejected));
        }

        return OkEnvelope(new MutationResultDto(taskId.ToString(), request.Decision, null, DateTimeOffset.UtcNow));
    }
}

internal static class RecruitmentProblems
{
    public static ProblemDetails From(HttpContext context, ApplicationRejectedException rejected)
    {
        var problem = new ProblemDetails
        {
            Title = rejected.Message,
            Status = StatusCodes.Status400BadRequest,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = rejected.Code;
        problem.Extensions["correlationId"] = context.TraceIdentifier;
        return problem;
    }
}
