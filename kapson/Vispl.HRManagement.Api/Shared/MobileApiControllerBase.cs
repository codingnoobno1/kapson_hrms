using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Shared;

[ApiController]
[Authorize(AuthenticationSchemes = MobileAuthenticationDefaults.BearerScheme)]
public abstract class MobileApiControllerBase : ControllerBase
{
    /// <summary>Client-supplied replay key. Every mutating action must forward it.</summary>
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    protected string? TenantKey => User.FindFirst("tenant_id")?.Value;

    /// <summary>
    /// The caller's own employee record. MobileTokenService emits this as "employee_number";
    /// self-service screens key off it, so it must match the claim the token actually carries.
    /// </summary>
    protected string? EmployeeId => User.FindFirst("employee_number")?.Value;

    /// <summary>
    /// The resolved caller for this request. Business services receive this and must never
    /// re-read claims or trust an identifier supplied in a request body.
    /// </summary>
    protected MobileCallerContext Caller => new(
        TenantKey ?? throw new MobileTenantClaimMissingException(),
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value
            ?? string.Empty,
        EmployeeId,
        User.FindAll("permission").Select(claim => claim.Value).ToArray(),
        HttpContext.TraceIdentifier,
        Request.Headers.TryGetValue(IdempotencyKeyHeader, out var key) ? key.ToString() : null,
        Request.Headers.IfMatch.Count > 0 ? Request.Headers.IfMatch.ToString().Trim('"') : null);

    protected ActionResult<ApiEnvelope<T>> OkEnvelope<T>(T data, long configurationVersion = 0)
    {
        var correlationId = HttpContext.TraceIdentifier;
        return Ok(new ApiEnvelope<T>(
            data,
            new ApiMetaDto(correlationId, DateTimeOffset.UtcNow, configurationVersion, [])));
    }

    protected ActionResult<ApiEnvelope<T>> OkEnvelope<T>(
        T data,
        IReadOnlyList<ApiWarningDto> warnings,
        long configurationVersion = 0) =>
        Ok(new ApiEnvelope<T>(
            data,
            new ApiMetaDto(HttpContext.TraceIdentifier, DateTimeOffset.UtcNow, configurationVersion, warnings)));

    /// <summary>
    /// Wraps a mutation result and stamps the new row version as an ETag so the next
    /// write from this client can be version-checked.
    /// </summary>
    protected ActionResult<ApiEnvelope<MutationResultDto>> MutationEnvelope(MutationResultDto result)
    {
        if (!string.IsNullOrWhiteSpace(result.RowVersion))
        {
            Response.Headers.ETag = $"\"{result.RowVersion}\"";
        }

        return OkEnvelope(result);
    }

    protected ActionResult<ApiEnvelope<T>> TenantClaimRequired<T>() =>
        new ObjectResult(MobileProblems.TenantClaimRequired(HttpContext))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
}

public sealed class MobileTenantClaimMissingException : Exception
{
    public MobileTenantClaimMissingException()
        : base("A valid tenant claim is required.")
    {
    }
}

internal static class MobileProblems
{
    public static ProblemDetails FeatureNotReady(
        HttpContext context,
        string module,
        IReadOnlyList<string> requiredComponents)
    {
        var problem = new ProblemDetails
        {
            Type = "https://api.hr.example.com/problems/feature-not-ready",
            Title = $"The {module} mobile feature is not ready.",
            Detail = "The route and its contract are registered, but the business and persistence "
                   + "implementation for this module has not been completed.",
            Status = StatusCodes.Status501NotImplemented,
            Instance = context.Request.Path
        };

        problem.Extensions["code"] = "FEATURE_NOT_READY";
        problem.Extensions["module"] = module;
        problem.Extensions["requiredComponents"] = requiredComponents;
        problem.Extensions["correlationId"] = context.TraceIdentifier;
        return problem;
    }

    public static ProblemDetails TenantClaimRequired(HttpContext context)
    {
        var problem = new ProblemDetails
        {
            Type = "https://api.hr.example.com/problems/tenant-claim-required",
            Title = "A valid tenant claim is required.",
            Status = StatusCodes.Status403Forbidden,
            Instance = context.Request.Path
        };

        problem.Extensions["code"] = "TENANT_CLAIM_REQUIRED";
        problem.Extensions["correlationId"] = context.TraceIdentifier;
        return problem;
    }
}

/// <summary>Minimal comment body shared by task, leave and record comment endpoints.</summary>
public sealed class AddRequestCommentBody
{
    public string Comment { get; set; } = string.Empty;
}
