using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.Identity.Admin;

namespace Vispl.HRManagement.Api.Shared.Controllers;

/// <summary>
/// Identify either by username (staff, who were given one) or by tenant plus email
/// (candidates, who were not). Exactly one form must be supplied.
/// </summary>
public sealed class TokenRequest : IValidatableObject
{
    [StringLength(80, MinimumLength = 3)]
    public string? UserName { get; set; }

    [StringLength(80, MinimumLength = 1)]
    public string? Tenant { get; set; }

    [EmailAddress, StringLength(256)]
    public string? Email { get; set; }

    [Required, StringLength(256, MinimumLength = 1)]
    public string Password { get; set; } = string.Empty;

    [StringLength(200)]
    public string? DeviceId { get; set; }

    public bool IsCandidateLogin =>
        string.IsNullOrWhiteSpace(UserName) &&
        !string.IsNullOrWhiteSpace(Tenant) &&
        !string.IsNullOrWhiteSpace(Email);

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasUserName = !string.IsNullOrWhiteSpace(UserName);
        var hasTenant = !string.IsNullOrWhiteSpace(Tenant);
        var hasEmail = !string.IsNullOrWhiteSpace(Email);

        if (hasUserName && (hasTenant || hasEmail))
        {
            yield return new ValidationResult(
                "Supply either userName, or tenant and email, but not both.",
                [nameof(UserName), nameof(Tenant), nameof(Email)]);
            yield break;
        }

        if (!hasUserName && !(hasTenant && hasEmail))
        {
            yield return new ValidationResult(
                "Supply either userName, or both tenant and email.",
                [nameof(UserName), nameof(Tenant), nameof(Email)]);
        }
    }
}

public sealed class RefreshRequest
{
    [Required, StringLength(200)]
    public string RefreshToken { get; set; } = string.Empty;

    [StringLength(200)]
    public string? DeviceId { get; set; }
}

public sealed class RevokeRequest
{
    [Required, StringLength(200)]
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Signs out every device in this token's rotation family.</summary>
    public bool AllDevices { get; set; }
}

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresIn,
    bool MustChangePassword,
    string DisplayName,
    string? TenantKey,
    string Portal,
    IReadOnlyList<string> Permissions);

[ApiController]
[AllowAnonymous]
[Route("api/mobile/v1/auth")]
public sealed class MobileAuthController(IMobileTokenService tokens) : ControllerBase
{
    [HttpPost("token")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> IssueAsync(
        [FromBody] TokenRequest request,
        CancellationToken cancellationToken)
    {
        var login = request.IsCandidateLogin
            ? LoginIdentifier.ForCandidate(request.Tenant!, request.Email!)
            : LoginIdentifier.ForUserName(request.UserName!);

        var result = await tokens.IssueAsync(login, request.Password, request.DeviceId, cancellationToken);

        return result.Succeeded ? Ok(Map(result.Grant!)) : Deny(result.Failure);
    }

    [HttpPost("refresh")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshAsync(
        [FromBody] RefreshRequest request,
        CancellationToken cancellationToken)
    {
        var result = await tokens.RefreshAsync(request.RefreshToken, request.DeviceId, cancellationToken);
        return result.Succeeded ? Ok(Map(result.Grant!)) : Deny(result.Failure);
    }

    /// <summary>
    /// Always returns 204. Revoking an unknown token is not an error, and reporting one
    /// would let a caller test whether a token exists.
    /// </summary>
    [HttpPost("revoke")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RevokeAsync(
        [FromBody] RevokeRequest request,
        CancellationToken cancellationToken)
    {
        await tokens.RevokeAsync(request.RefreshToken, request.AllDevices, cancellationToken);
        return NoContent();
    }

    private static TokenResponse Map(TokenGrant grant) => new(
        grant.AccessToken,
        grant.RefreshToken,
        grant.TokenType,
        grant.ExpiresInSeconds,
        grant.MustChangePassword,
        grant.DisplayName,
        grant.TenantKey,
        grant.Portal,
        grant.Permissions);

    private IActionResult Deny(TokenFailure failure)
    {
        // Invalid credentials, an unknown user and a portal that cannot use the app all
        // return the same message, so the endpoint reveals nothing about the account.
        var (status, code, title) = failure switch
        {
            TokenFailure.LockedOut => (
                StatusCodes.Status423Locked,
                "ACCOUNT_LOCKED",
                "Too many failed attempts. Try again later."),
            // 403, not 401: the credentials were right. Saying so plainly is what stops
            // somebody retyping a correct password over and over.
            TokenFailure.MobileAccessNotGranted => (
                StatusCodes.Status403Forbidden,
                "MOBILE_ACCESS_NOT_GRANTED",
                "This account has not been given mobile access. Ask HR or your administrator to enable it."),
            TokenFailure.Disabled => (
                StatusCodes.Status403Forbidden,
                "ACCOUNT_DISABLED",
                "This account is not active."),
            TokenFailure.InvalidRefreshToken => (
                StatusCodes.Status401Unauthorized,
                "INVALID_REFRESH_TOKEN",
                "Sign in again."),
            _ => (
                StatusCodes.Status401Unauthorized,
                "INVALID_CREDENTIALS",
                "That username and password combination is not valid.")
        };

        var problem = new ProblemDetails
        {
            Title = title,
            Status = status,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = HttpContext.TraceIdentifier;

        return new ObjectResult(problem) { StatusCode = status };
    }
}
