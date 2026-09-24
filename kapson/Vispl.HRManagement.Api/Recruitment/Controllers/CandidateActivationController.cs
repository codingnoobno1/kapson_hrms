using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.Identity.Candidate;

namespace Vispl.HRManagement.Api.Recruitment.Controllers;

public sealed class ActivationRequest
{
    [Required, StringLength(80)]
    public string Tenant { get; set; } = string.Empty;

    [Required, EmailAddress, StringLength(256)]
    public string Email { get; set; } = string.Empty;
}

public sealed class ActivateAccountRequest
{
    [Required, StringLength(80)]
    public string Tenant { get; set; } = string.Empty;

    [Required, StringLength(200)]
    public string Token { get; set; } = string.Empty;

    [Required, StringLength(256, MinimumLength = 10)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>
/// Public account activation for applicants. An application creates the candidate record;
/// this is how that person claims it and sets a password.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public/v1/candidate")]
public sealed class CandidateActivationController(
    ICandidateActivationService activation,
    IHostEnvironment environment,
    ILogger<CandidateActivationController> logger) : ControllerBase
{
    /// <summary>
    /// Always returns 202, whether or not a candidate exists for that address. Reporting the
    /// difference would turn this into an address-checking oracle.
    /// </summary>
    [HttpPost("activation-link")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> RequestAsync(
        [FromBody] ActivationRequest request,
        CancellationToken cancellationToken)
    {
        var token = await activation.RequestAsync(request.Tenant, request.Email, cancellationToken);

        if (token is not null && environment.IsDevelopment())
        {
            // No mail transport is wired yet. In Development the link is logged so the flow can
            // be exercised; in every other environment the token is created and simply dropped
            // until a mailer delivers it, rather than being exposed here.
            logger.LogWarning(
                "Development activation link for {Email}: token={Token}", request.Email, token);
        }

        return Accepted(new { status = "If that address matches an application, a link has been sent." });
    }

    [HttpPost("activate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateAsync(
        [FromBody] ActivateAccountRequest request,
        CancellationToken cancellationToken)
    {
        var result = await activation.ActivateAsync(
            request.Tenant, request.Token, request.Password, cancellationToken);

        if (result.Succeeded)
        {
            return NoContent();
        }

        var problem = new ProblemDetails
        {
            Title = result.Errors.Count > 0 ? result.Errors[0] : "This link is not valid.",
            Status = StatusCodes.Status400BadRequest,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = result.Failure.ToString();
        problem.Extensions["errors"] = result.Errors;
        problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;

        return BadRequest(problem);
    }
}
