using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.BM.ITF.Mobile;

namespace Vispl.HRManagement.Api.Shared;

/// <summary>
/// Translates business-layer failures into the mobile HTTP contract defined in
/// <c>backend.md</c> §6. Controllers therefore contain no try/catch and no status-code
/// mapping of their own.
/// </summary>
public sealed class MobileApiExceptionFilter(ILogger<MobileApiExceptionFilter> logger) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        var problem = context.Exception switch
        {
            MobileFeatureNotReadyException notReady => MobileProblems.FeatureNotReady(
                context.HttpContext,
                notReady.Module,
                notReady.RequiredComponents),

            MobileTenantClaimMissingException => MobileProblems.TenantClaimRequired(context.HttpContext),

            MobileAccessDeniedException denied => Problem(
                context.HttpContext, StatusCodes.Status403Forbidden,
                "PERMISSION_REQUIRED", denied.Message),

            MobileValidationException invalid => Problem(
                context.HttpContext, StatusCodes.Status400BadRequest,
                invalid.Code, invalid.Message),

            MobileNotFoundException missing => Problem(
                context.HttpContext, StatusCodes.Status404NotFound,
                "NOT_FOUND", missing.Message),

            _ => null
        };

        if (problem is null)
        {
            return;
        }

        if (problem.Status == StatusCodes.Status501NotImplemented)
        {
            logger.LogInformation(
                "Mobile route {Path} reached an unimplemented module. Correlation {CorrelationId}.",
                context.HttpContext.Request.Path,
                context.HttpContext.TraceIdentifier);
        }

        context.Result = new ObjectResult(problem) { StatusCode = problem.Status };
        context.ExceptionHandled = true;
    }

    private static ProblemDetails Problem(HttpContext context, int status, string code, string title)
    {
        var problem = new ProblemDetails
        {
            Title = title,
            Status = status,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = code;
        problem.Extensions["correlationId"] = context.TraceIdentifier;
        return problem;
    }
}
