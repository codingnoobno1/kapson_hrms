using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Vispl.HRManagement.Api.Shared;

public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        Policy = MobileAuthenticationDefaults.PermissionPolicyPrefix + permission;
    }
}

public sealed record MobilePermissionRequirement(string Permission) : IAuthorizationRequirement;

public sealed class MobilePermissionHandler : AuthorizationHandler<MobilePermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MobilePermissionRequirement requirement)
    {
        if (context.User.Claims.Any(claim =>
                claim.Type == "permission" &&
                string.Equals(claim.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public sealed class MobilePermissionPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public MobilePermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(
                MobileAuthenticationDefaults.PermissionPolicyPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return base.GetPolicyAsync(policyName);
        }

        var permission = policyName[MobileAuthenticationDefaults.PermissionPolicyPrefix.Length..];
        var policy = new AuthorizationPolicyBuilder(MobileAuthenticationDefaults.BearerScheme)
            .RequireAuthenticatedUser()
            .AddRequirements(new MobilePermissionRequirement(permission))
            .Build();

        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
