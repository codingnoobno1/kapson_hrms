using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.Identity.Admin;

public sealed class AccessRoleService(
    AdminIdentityDbContext dbContext,
    RoleManager<HrRole> roleManager,
    ITenantService tenantService) : IAccessRoleService
{
    private sealed record SystemRoleTemplate(
        AccountPortal Portal,
        string Code,
        string Name,
        string Description,
        Func<PermissionDefinitionDto, bool> PermissionFilter);

    private static readonly SystemRoleTemplate[] SystemRoles =
    [
        new(AccountPortal.Hr, "HR_ADMINISTRATOR", "HR Administrator",
            "Full client HR access, including tenant account and custom-role administration.", _ => true),
        new(AccountPortal.Hr, "HR_EXECUTIVE", "HR Executive",
            "Day-to-day employee, recruitment, attendance and leave operations.", permission =>
                !permission.Key.StartsWith("PAYROLL.", StringComparison.Ordinal) &&
                !permission.Key.StartsWith("ACCOUNTS.", StringComparison.Ordinal) &&
                !permission.Key.StartsWith("ROLES.", StringComparison.Ordinal)),
        new(AccountPortal.Hr, "RECRUITER", "Recruiter",
            "Recruitment pipeline, vacancy publishing and employee read access.", permission =>
                permission.Key.StartsWith("RECRUITMENT.", StringComparison.Ordinal) ||
                permission.Key == "EMPLOYEES.VIEW" || permission.Key == "HR.DASHBOARD.VIEW"),
        new(AccountPortal.Employee, "EMPLOYEE", "Employee",
            "Standard employee self-service access.", permission =>
                permission.Key.StartsWith("SELF.", StringComparison.Ordinal)),
        new(AccountPortal.Employee, "REPORTING_MANAGER", "Reporting Manager",
            "Employee self-service plus visibility and leave approval for direct reports.", _ => true)
    ];

    public IReadOnlyList<PermissionDefinitionDto> GetPermissionCatalog(AccountPortal portal) =>
        HrPermissionCatalog.ForPortal(portal);

    public async Task<IReadOnlyList<AccessRoleDto>> GetAllAsync(
        string tenantKey,
        AccountPortal? portal = null,
        CancellationToken cancellationToken = default)
    {
        tenantKey = await ValidateTenantAsync(tenantKey, cancellationToken);
        await EnsureSystemRolesAsync(tenantKey, cancellationToken);

        var roles = await dbContext.Roles.AsNoTracking()
            .Where(role => role.TenantKey == tenantKey &&
                (!portal.HasValue || role.Portal == portal.Value))
            .OrderBy(role => role.Portal)
            .ThenByDescending(role => role.IsSystem)
            .ThenBy(role => role.DisplayName)
            .ToListAsync(cancellationToken);

        var roleIds = roles.Select(role => role.Id).ToList();
        var permissions = await dbContext.RolePermissions.AsNoTracking()
            .Where(permission => roleIds.Contains(permission.RoleId))
            .GroupBy(permission => permission.RoleId)
            .ToDictionaryAsync(
                group => group.Key,
                group => group.Select(item => item.PermissionKey).OrderBy(key => key).ToList(),
                cancellationToken);
        var counts = await dbContext.UserRoles.AsNoTracking()
            .Where(userRole => roleIds.Contains(userRole.RoleId))
            .GroupBy(userRole => userRole.RoleId)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);

        return roles.Select(role => ToDto(
            role,
            permissions.GetValueOrDefault(role.Id, []),
            counts.GetValueOrDefault(role.Id))).ToList();
    }

    public async Task<AccessRoleDto> SaveAsync(
        SaveAccessRoleRequest request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (request.Portal == AccountPortal.Platform)
        {
            throw new InvalidOperationException("Platform roles cannot be created from client access management.");
        }

        var tenantKey = await ValidateTenantAsync(request.TenantKey, cancellationToken);
        var roleCode = request.RoleCode.Trim().ToUpperInvariant();
        var permissions = request.PermissionKeys
            .Select(key => key.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var invalidPermission = permissions.FirstOrDefault(key => !HrPermissionCatalog.IsValid(key, request.Portal));
        if (invalidPermission is not null)
        {
            throw new InvalidOperationException($"Permission '{invalidPermission}' does not belong to the {request.Portal} portal.");
        }

        HrRole role;
        var action = "ROLE_CREATED";
        if (string.IsNullOrWhiteSpace(request.Id))
        {
            var duplicate = await dbContext.Roles.AnyAsync(item =>
                item.TenantKey == tenantKey && item.Portal == request.Portal && item.RoleCode == roleCode,
                cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException($"Role code '{roleCode}' already exists for this client and portal.");
            }

            role = new HrRole
            {
                Name = BuildInternalName(tenantKey, request.Portal, roleCode),
                TenantKey = tenantKey,
                Portal = request.Portal,
                RoleCode = roleCode,
                DisplayName = request.DisplayName.Trim(),
                Description = NormalizeOptional(request.Description),
                IsSystem = false,
                IsActive = request.IsActive,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            EnsureSucceeded(await roleManager.CreateAsync(role));
        }
        else
        {
            role = await roleManager.FindByIdAsync(request.Id)
                ?? throw new InvalidOperationException("Role was not found.");
            if (role.TenantKey != tenantKey)
            {
                throw new InvalidOperationException("The selected role belongs to another client.");
            }
            if (role.IsSystem)
            {
                throw new InvalidOperationException("System roles are protected and cannot be modified.");
            }
            if (role.Portal != request.Portal || role.RoleCode != roleCode)
            {
                throw new InvalidOperationException("A role's client, portal and code cannot change after creation.");
            }

            role.DisplayName = request.DisplayName.Trim();
            role.Description = NormalizeOptional(request.Description);
            role.IsActive = request.IsActive;
            role.UpdatedUtc = DateTimeOffset.UtcNow;
            EnsureSucceeded(await roleManager.UpdateAsync(role));
            action = "ROLE_UPDATED";
        }

        var existingPermissions = await dbContext.RolePermissions
            .Where(permission => permission.RoleId == role.Id)
            .ToListAsync(cancellationToken);
        dbContext.RolePermissions.RemoveRange(existingPermissions);
        dbContext.RolePermissions.AddRange(permissions.Select(key => new RolePermissionEntity
        {
            RoleId = role.Id,
            PermissionKey = key,
            CreatedUtc = DateTimeOffset.UtcNow
        }));
        AddAudit(tenantKey, actorUserId, action, "Role", role.Id, new
        {
            role.RoleCode,
            role.DisplayName,
            Portal = role.Portal.ToString(),
            Permissions = permissions
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        var accountCount = await dbContext.UserRoles.CountAsync(item => item.RoleId == role.Id, cancellationToken);
        return ToDto(role, permissions, accountCount);
    }

    public async Task DeleteAsync(
        string roleId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId)
            ?? throw new InvalidOperationException("Role was not found.");
        if (role.IsSystem)
        {
            throw new InvalidOperationException("System roles are protected and cannot be deleted.");
        }

        var accountCount = await dbContext.UserRoles.CountAsync(item => item.RoleId == role.Id, cancellationToken);
        if (accountCount > 0)
        {
            throw new InvalidOperationException($"Remove this role from {accountCount} account(s) before deleting it.");
        }

        AddAudit(role.TenantKey, actorUserId, "ROLE_DELETED", "Role", role.Id, new
        {
            role.RoleCode,
            role.DisplayName,
            Portal = role.Portal.ToString()
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        EnsureSucceeded(await roleManager.DeleteAsync(role));
    }

    private async Task EnsureSystemRolesAsync(string tenantKey, CancellationToken cancellationToken)
    {
        foreach (var template in SystemRoles)
        {
            var expectedKeys = HrPermissionCatalog.ForPortal(template.Portal)
                .Where(template.PermissionFilter)
                .Select(permission => permission.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existing = await dbContext.Roles.SingleOrDefaultAsync(role =>
                role.TenantKey == tenantKey && role.Portal == template.Portal && role.RoleCode == template.Code,
                cancellationToken);

            if (existing is not null)
            {
                // A protected role is defined by its filter, not by whatever was granted the day
                // it was created. Without this top-up, every permission added to the catalogue
                // afterwards is silently missing: "HR Administrator" would mean full access as of
                // some past release, and the endpoints behind newer permissions would answer 403
                // to the very role named for holding them.
                //
                // Only system roles are reconciled. A custom role is somebody's deliberate
                // choice and must never be widened behind their back.
                await ReconcileSystemRoleAsync(existing, expectedKeys, cancellationToken);
                continue;
            }

            var role = new HrRole
            {
                Name = BuildInternalName(tenantKey, template.Portal, template.Code),
                TenantKey = tenantKey,
                Portal = template.Portal,
                RoleCode = template.Code,
                DisplayName = template.Name,
                Description = template.Description,
                IsSystem = true,
                IsActive = true,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };
            EnsureSucceeded(await roleManager.CreateAsync(role));

            dbContext.RolePermissions.AddRange(expectedKeys.Select(key => new RolePermissionEntity
            {
                RoleId = role.Id,
                PermissionKey = key,
                CreatedUtc = DateTimeOffset.UtcNow
            }));
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Brings a protected role's grants back in line with the definition it is named for.
    ///
    /// Adds what the filter now includes, and drops keys that have left the catalogue entirely.
    /// A key still in the catalogue but excluded by the filter is also dropped, because for a
    /// system role the filter is the definition — a role called "Recruiter" that has quietly
    /// accumulated payroll rights is worse than one that is merely out of date.
    /// </summary>
    private async Task ReconcileSystemRoleAsync(
        HrRole role, HashSet<string> expectedKeys, CancellationToken cancellationToken)
    {
        var current = await dbContext.RolePermissions
            .Where(permission => permission.RoleId == role.Id)
            .ToListAsync(cancellationToken);

        var currentKeys = current
            .Select(permission => permission.PermissionKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = expectedKeys.Where(key => !currentKeys.Contains(key)).ToList();
        var surplus = current
            .Where(permission => !expectedKeys.Contains(permission.PermissionKey))
            .ToList();

        if (missing.Count == 0 && surplus.Count == 0)
        {
            return;
        }

        dbContext.RolePermissions.AddRange(missing.Select(key => new RolePermissionEntity
        {
            RoleId = role.Id,
            PermissionKey = key,
            CreatedUtc = DateTimeOffset.UtcNow
        }));

        dbContext.RolePermissions.RemoveRange(surplus);

        role.UpdatedUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> ValidateTenantAsync(string tenantKey, CancellationToken cancellationToken)
    {
        tenantKey = tenantKey.Trim().ToLowerInvariant();
        var tenant = await tenantService.GetByKeyAsync(tenantKey, cancellationToken)
            ?? throw new InvalidOperationException("The selected client does not exist.");
        if (!tenant.IsActive)
        {
            throw new InvalidOperationException("The selected client's licence is inactive.");
        }
        return tenant.TenantKey;
    }

    private void AddAudit(
        string? tenantKey,
        string? actorUserId,
        string action,
        string subjectType,
        string subjectId,
        object details) => dbContext.IdentityAudits.Add(new IdentityAuditEntity
    {
        TenantKey = tenantKey,
        ActorUserId = actorUserId,
        Action = action,
        SubjectType = subjectType,
        SubjectId = subjectId,
        DetailJson = JsonSerializer.Serialize(details),
        OccurredUtc = DateTimeOffset.UtcNow
    });

    private static string BuildInternalName(string tenantKey, AccountPortal portal, string roleCode) =>
        $"TENANT::{tenantKey.ToUpperInvariant()}::{portal.ToString().ToUpperInvariant()}::{roleCode}";

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }

    private static AccessRoleDto ToDto(HrRole role, IEnumerable<string> permissions, int accountCount) => new()
    {
        Id = role.Id,
        TenantKey = role.TenantKey ?? string.Empty,
        Portal = role.Portal,
        RoleCode = role.RoleCode,
        DisplayName = role.DisplayName,
        Description = role.Description,
        IsSystem = role.IsSystem,
        IsActive = role.IsActive,
        AccountCount = accountCount,
        CreatedUtc = role.CreatedUtc,
        UpdatedUtc = role.UpdatedUtc,
        PermissionKeys = permissions.OrderBy(key => key).ToList()
    };
}
