using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using Vispl.HRManagement.BM.ITF.Mobile;

namespace Vispl.HRManagement.UI.Components.Hr.Shared;

/// <summary>
/// Builds the caller context the business services expect, from the HR portal's cookie.
///
/// The API builds the same context from a bearer token. Both paths end at the same services,
/// which is what keeps the portal and the mobile apps enforcing one set of rules rather than
/// two — a page must never make its own authorisation decision.
/// </summary>
public sealed class HrPortalCaller(AuthenticationStateProvider authentication)
{
    /// <summary>
    /// Resolves the signed-in HR user.
    ///
    /// The cookie carries <c>tenant_key</c> where the mobile token carries <c>tenant_id</c>;
    /// both are read so this works whichever issued the principal.
    /// </summary>
    public async Task<MobileCallerContext> GetAsync(string? correlationId = null)
    {
        var state = await authentication.GetAuthenticationStateAsync();
        var user = state.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            throw new InvalidOperationException("The HR portal requires a signed-in user.");
        }

        var tenantKey = user.FindFirst("tenant_key")?.Value
                        ?? user.FindFirst("tenant_id")?.Value;

        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            throw new InvalidOperationException(
                "This account is not attached to a client, so there is no data it can be shown.");
        }

        return new MobileCallerContext(
            tenantKey,
            user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty,
            user.FindFirst("employee_number")?.Value,
            user.FindAll("permission").Select(claim => claim.Value).ToArray(),
            correlationId ?? Guid.NewGuid().ToString("n"));
    }

    public async Task<string> GetDisplayNameAsync()
    {
        var state = await authentication.GetAuthenticationStateAsync();
        return state.User.FindFirst("display_name")?.Value
               ?? state.User.Identity?.Name
               ?? "HR user";
    }

    public async Task<bool> HasAsync(string permission)
    {
        var state = await authentication.GetAuthenticationStateAsync();
        return state.User.FindAll("permission")
            .Any(claim => string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
    }
}
