using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace Vispl.HRManagement.Identity.Admin;

public sealed class AdminClaimsPrincipalFactory
    : UserClaimsPrincipalFactory<AdminUser, HrRole>
{
    private readonly UserManager<AdminUser> userManager;
    private readonly AdminIdentityDbContext dbContext;

    public AdminClaimsPrincipalFactory(
        UserManager<AdminUser> userManager,
        RoleManager<HrRole> roleManager,
        IOptions<IdentityOptions> options,
        AdminIdentityDbContext dbContext)
        : base(userManager, roleManager, options)
    {
        this.userManager = userManager;
        this.dbContext = dbContext;
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AdminUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("must_change_password", user.MustChangePassword ? "true" : "false"));
        identity.AddClaim(new Claim("display_name", user.DisplayName));

        if (user.PreviousLoginUtc is { } previousLogin)
        {
            identity.AddClaim(new Claim("previous_login_utc", previousLogin.ToString("O", System.Globalization.CultureInfo.InvariantCulture)));
        }
        identity.AddClaim(new Claim("account_portal", user.Portal.ToString()));
        identity.AddClaim(new Claim("account_active", user.IsActive ? "true" : "false"));
        if (!string.IsNullOrWhiteSpace(user.TenantKey))
        {
            identity.AddClaim(new Claim("tenant_key", user.TenantKey));
        }

        // The mobile token has carried this since the beginning; the cookie did not, so every
        // portal caller looked like somebody with no employee record of their own. Anything that
        // asks "is this the caller's own record" — self-service reads, policy acknowledgement —
        // silently answered no. Both paths now build the same caller context, which is what
        // docs/employee-app/portal-integration.md §2 already claims they do.
        if (!string.IsNullOrWhiteSpace(user.EmployeeNumber))
        {
            identity.AddClaim(new Claim("employee_number", user.EmployeeNumber));
        }

        var roleNames = await userManager.GetRolesAsync(user);
        var roleIds = await dbContext.Roles
            .Where(role => role.Name != null && roleNames.Contains(role.Name))
            .Select(role => role.Id)
            .ToListAsync();
        var permissions = await dbContext.RolePermissions
            .Where(permission => roleIds.Contains(permission.RoleId))
            .Select(permission => permission.PermissionKey)
            .Distinct()
            .ToListAsync();
        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim("permission", permission));
        }

        return identity;
    }
}
