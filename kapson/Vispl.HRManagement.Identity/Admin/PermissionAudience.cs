using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.Identity.Admin;

/// <summary>
/// Resolves "everyone in this tenant who holds a permission" so a module can address an
/// audience without knowing anything about roles or the user store.
/// </summary>
public sealed class PermissionAudience(AdminIdentityDbContext dbContext) : IPermissionAudience
{
    public async Task<IReadOnlyList<string>> GetAccountIdsWithPermissionAsync(
        string tenantKey, string permission, CancellationToken cancellationToken = default)
    {
        // Roles that grant the permission, scoped to this tenant.
        var roleIds = await dbContext.RolePermissions
            .Where(grant => grant.PermissionKey == permission)
            .Join(
                dbContext.Roles.Where(role => role.TenantKey == tenantKey && role.IsActive),
                grant => grant.RoleId,
                role => role.Id,
                (grant, role) => role.Id)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
        {
            return [];
        }

        // Only active accounts are notified. A disabled account being messaged would be a
        // leak of activity to someone who should no longer see it.
        return await dbContext.UserRoles
            .Where(assignment => roleIds.Contains(assignment.RoleId))
            .Join(
                dbContext.Users.Where(user =>
                    user.TenantKey == tenantKey &&
                    user.IsActive &&
                    user.Portal != AccountPortal.Candidate),
                assignment => assignment.UserId,
                user => user.Id,
                (assignment, user) => user.Id)
            .Distinct()
            .ToListAsync(cancellationToken);
    }
}
