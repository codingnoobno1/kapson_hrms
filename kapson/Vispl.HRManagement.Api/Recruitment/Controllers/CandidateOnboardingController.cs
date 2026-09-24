using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Contracts.Recruitment;

namespace Vispl.HRManagement.Api.Recruitment.Controllers;

public sealed class OfferResponseRequest
{
    public bool Accept { get; set; }
    public DateOnly? JoiningOn { get; set; }
    public string? Reason { get; set; }
}

public sealed class FormTaskRequest
{
    public Dictionary<string, string> Values { get; set; } = [];
}

/// <summary>
/// Candidate-owned routes. Authorised by the candidate identity in the token, never by a
/// permission and never by an id supplied in the request body.
/// </summary>
[Route("api/candidate/v1")]
public sealed class CandidateOnboardingController(IOnboardingService onboarding) : MobileApiControllerBase
{
    private long? CandidateId =>
        long.TryParse(User.FindFirst("candidate_id")?.Value, out var value) ? value : null;

    [HttpPost("applications/{applicationId:long}/offer")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> RespondToOfferAsync(
        long applicationId,
        [FromBody] OfferResponseRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<MutationResultDto>();
        }

        if (CandidateId is not { } candidateId)
        {
            return Forbid();
        }

        try
        {
            if (request.Accept)
            {
                var onboardingId = await onboarding.AcceptOfferAsync(
                    TenantKey, applicationId, candidateId, request.JoiningOn, cancellationToken);

                return OkEnvelope(new MutationResultDto(
                    onboardingId.ToString(), ApplicationStages.Onboarding, null, DateTimeOffset.UtcNow,
                    "Offer accepted. Your onboarding checklist is ready."));
            }

            await onboarding.DeclineOfferAsync(TenantKey, applicationId, candidateId, request.Reason, cancellationToken);
            return OkEnvelope(new MutationResultDto(
                applicationId.ToString(), ApplicationStages.OfferDeclined, null, DateTimeOffset.UtcNow));
        }
        catch (ApplicationRejectedException rejected)
        {
            return BadRequest(RecruitmentProblems.From(HttpContext, rejected));
        }
    }

    /// <summary>
    /// Everything this candidate may see about themselves, resolved from their own token.
    /// The entry point for somebody arriving from an email, who knows no ids at all.
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<ApiEnvelope<CandidateProgressDto>>> MeAsync(
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<CandidateProgressDto>();
        }

        if (CandidateId is not { } candidateId)
        {
            return Forbid();
        }

        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(await onboarding.GetCandidateProgressAsync(TenantKey, candidateId, cancellationToken));
    }

    [HttpGet("onboarding/{id:long}")]
    public async Task<ActionResult<ApiEnvelope<OnboardingDto>>> GetAsync(long id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<OnboardingDto>();
        }

        if (CandidateId is not { } candidateId)
        {
            return Forbid();
        }

        var result = await onboarding.GetAsync(TenantKey, id, cancellationToken);

        // A candidate may only read their own checklist.
        if (result is null || result.CandidateId != candidateId)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "no-store";
        return OkEnvelope(result);
    }

    [HttpPost("onboarding/{id:long}/tasks/{taskId:long}/form")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SubmitFormAsync(
        long id,
        long taskId,
        [FromBody] FormTaskRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<MutationResultDto>();
        }

        if (CandidateId is not { } candidateId)
        {
            return Forbid();
        }

        try
        {
            await onboarding.SubmitFormTaskAsync(TenantKey, id, taskId, candidateId, request.Values, cancellationToken);
        }
        catch (ApplicationRejectedException rejected)
        {
            return BadRequest(RecruitmentProblems.From(HttpContext, rejected));
        }

        return OkEnvelope(new MutationResultDto(
            taskId.ToString(), OnboardingTaskStatuses.Submitted, null, DateTimeOffset.UtcNow));
    }

    [HttpPost("onboarding/{id:long}/tasks/{taskId:long}/document")]
    [RequestSizeLimit(ApplicationDocumentRules.MaximumFileBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ApplicationDocumentRules.MaximumFileBytes)]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> SubmitDocumentAsync(
        long id,
        long taskId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(TenantKey))
        {
            return TenantClaimRequired<MutationResultDto>();
        }

        if (CandidateId is not { } candidateId)
        {
            return Forbid();
        }

        if (!Request.HasFormContentType || Request.Form.Files.Count == 0)
        {
            return BadRequest(RecruitmentProblems.From(
                HttpContext, new ApplicationRejectedException("FILE_REQUIRED", "Attach a file.")));
        }

        var posted = Request.Form.Files[0];
        await using var content = posted.OpenReadStream();

        // Buffered so the validator can read the header and rewind.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        buffer.Position = 0;

        try
        {
            await onboarding.SubmitDocumentTaskAsync(
                TenantKey, id, taskId, candidateId,
                new UploadedFile("Task", posted.FileName, posted.ContentType ?? "application/octet-stream", buffer.Length, buffer),
                cancellationToken);
        }
        catch (ApplicationRejectedException rejected)
        {
            return BadRequest(RecruitmentProblems.From(HttpContext, rejected));
        }

        return OkEnvelope(new MutationResultDto(
            taskId.ToString(), OnboardingTaskStatuses.Submitted, null, DateTimeOffset.UtcNow));
    }
}
