using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.Contracts.Recruitment;

namespace Vispl.HRManagement.Api.Recruitment.Controllers;

/// <summary>
/// Public, unauthenticated career-site surface. Everything here is reachable by anyone, so it
/// carries its own size caps and leans on the service layer for content validation.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/v1")]
public sealed class PublicApplyController(
    IApplicationService applications,
    Vispl.HRManagement.BM.ITF.Candidate.ICandidateInvitation invitation) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [HttpGet("openings/{slug}")]
    [ProducesResponseType<PublicOpeningDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublicOpeningDto>> GetOpeningAsync(
        [FromQuery] string tenant,
        string slug,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenant))
        {
            return BadRequest(Problem("TENANT_REQUIRED", "A company must be specified."));
        }

        var opening = await applications.GetPublicOpeningAsync(tenant, slug, cancellationToken);
        return opening is null ? NotFound() : Ok(opening);
    }

    /// <summary>
    /// Multipart submission: a JSON "payload" part plus one file part per document category.
    /// </summary>
    [HttpPost("openings/{slug}/apply")]
    [RequestSizeLimit(ApplicationDocumentRules.MaximumSubmissionBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = ApplicationDocumentRules.MaximumSubmissionBytes)]
    [ProducesResponseType<SubmitApplicationResultDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SubmitApplicationResultDto>> ApplyAsync(
        [FromQuery] string tenant,
        string slug,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenant))
        {
            return BadRequest(Problem("TENANT_REQUIRED", "A company must be specified."));
        }

        if (!Request.HasFormContentType)
        {
            return BadRequest(Problem("MULTIPART_REQUIRED", "The application must be sent as multipart/form-data."));
        }

        var form = await Request.ReadFormAsync(cancellationToken);

        var payload = form["payload"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return BadRequest(Problem("PAYLOAD_REQUIRED", "The payload part is missing."));
        }

        SubmitApplicationRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<SubmitApplicationRequest>(payload, Json);
        }
        catch (JsonException)
        {
            return BadRequest(Problem("PAYLOAD_INVALID", "The payload part is not valid JSON."));
        }

        if (request is null)
        {
            return BadRequest(Problem("PAYLOAD_INVALID", "The payload part is empty."));
        }

        if (!TryValidateModel(request))
        {
            return ValidationProblem(ModelState);
        }

        // Each file part is named for the document category it satisfies.
        var files = form.Files
            .Select(file => new UploadedFile(
                file.Name,
                file.FileName,
                file.ContentType ?? "application/octet-stream",
                file.Length,
                file.OpenReadStream()))
            .ToList();

        if (files.Count == 0)
        {
            return BadRequest(Problem("RESUME_REQUIRED", "A résumé is required."));
        }

        try
        {
            var result = await applications.SubmitAsync(tenant, slug, request, files, cancellationToken);

            // Applying is what earns a candidate account: the offer they will later be asked
            // to accept can only be accepted while signed in. The career site's apply page
            // makes the same call, so both ways in behave identically — which they did not
            // when this orchestration lived here.
            await invitation.InviteAsync(tenant, request.Email, request.FullName, cancellationToken);

            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (ApplicationRejectedException rejected)
        {
            return BadRequest(Problem(rejected.Code, rejected.Message));
        }
        finally
        {
            foreach (var file in files)
            {
                await file.Content.DisposeAsync();
            }
        }
    }

    private ProblemDetails Problem(string code, string message) => new()
    {
        Title = message,
        Status = StatusCodes.Status400BadRequest,
        Instance = HttpContext.Request.Path,
        Extensions =
        {
            ["code"] = code,
            ["correlationId"] = HttpContext.TraceIdentifier
        }
    };
}
