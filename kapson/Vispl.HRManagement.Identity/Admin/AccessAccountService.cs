using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.Identity.Admin;

public sealed class AccessAccountService(
    AdminIdentityDbContext dbContext,
    UserManager<AdminUser> userManager,
    ITenantService tenantService) : IAccessAccountService
{
    public async Task<IReadOnlyList<AccessAccountDto>> GetAllAsync(
        string tenantKey,
        AccountPortal? portal = null,
        CancellationToken cancellationToken = default)
    {
        tenantKey = await ValidateTenantAsync(tenantKey, cancellationToken);
        var users = await dbContext.Users.AsNoTracking()
            .Where(user => user.TenantKey == tenantKey &&
                user.Portal != AccountPortal.Platform &&
                (!portal.HasValue || user.Portal == portal.Value))
            .OrderBy(user => user.DisplayName)
            .ToListAsync(cancellationToken);
        return await MapAsync(users, cancellationToken);
    }

    public async Task<AccessAccountDto> CreateAsync(
        CreateAccessAccountRequest request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (request.Portal == AccountPortal.Platform)
        {
            throw new InvalidOperationException("Platform administrator accounts are provisioned separately.");
        }
        var tenantKey = await ValidateTenantAsync(request.TenantKey, cancellationToken);
        var roles = await ResolveRolesAsync(tenantKey, request.Portal, request.RoleIds, cancellationToken);
        await EnsureEmailAvailableAsync(request.Email, null, cancellationToken);
        await EnsureEmployeeNumberAvailableAsync(tenantKey, request.EmployeeNumber, null, cancellationToken);

        var existingUserName = await userManager.FindByNameAsync(request.UserName.Trim());
        if (existingUserName is not null)
        {
            throw new InvalidOperationException($"Username '{request.UserName.Trim()}' is already in use.");
        }

        AdminUser? createdUser = null;
        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var user = new AdminUser
            {
                TenantKey = tenantKey,
                Portal = request.Portal,
                UserName = request.UserName.Trim(),
                Email = request.Email.Trim(),
                EmailConfirmed = true,
                DisplayName = request.DisplayName.Trim(),
                EmployeeNumber = NormalizeOptional(request.EmployeeNumber),
                IsActive = request.IsActive,
                MobileAccessEnabled = request.MobileAccessEnabled,
                MustChangePassword = true,
                LockoutEnabled = true,
                CreatedUtc = now,
                UpdatedUtc = now,
                CreatedByUserId = actorUserId
            };

            EnsureSucceeded(await userManager.CreateAsync(user, request.TemporaryPassword));
            EnsureSucceeded(await userManager.AddToRolesAsync(user, roles.Select(role => role.Name!)));
            AddAudit(tenantKey, actorUserId, "ACCOUNT_CREATED", user.Id, new
            {
                user.UserName,
                user.Email,
                user.DisplayName,
                Portal = user.Portal.ToString(),
                Roles = roles.Select(role => role.RoleCode),
                user.IsActive
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            createdUser = user;
        });
        return (await MapAsync([createdUser!], cancellationToken)).Single();
    }

    public async Task<AccessAccountDto> UpdateAsync(
        UpdateAccessAccountRequest request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(request.Id)
            ?? throw new InvalidOperationException("Account was not found.");
        if (user.Portal == AccountPortal.Platform || string.IsNullOrWhiteSpace(user.TenantKey))
        {
            throw new InvalidOperationException("Platform administrator accounts cannot be changed here.");
        }

        var roles = await ResolveRolesAsync(user.TenantKey, user.Portal, request.RoleIds, cancellationToken);
        await EnsureEmailAvailableAsync(request.Email, user.Id, cancellationToken);
        await EnsureEmployeeNumberAvailableAsync(user.TenantKey, request.EmployeeNumber, user.Id, cancellationToken);

        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            user.DisplayName = request.DisplayName.Trim();
            user.Email = request.Email.Trim();
            user.EmployeeNumber = NormalizeOptional(request.EmployeeNumber);
            user.IsActive = request.IsActive;
            user.MobileAccessEnabled = request.MobileAccessEnabled;
            user.UpdatedUtc = DateTimeOffset.UtcNow;
            EnsureSucceeded(await userManager.UpdateAsync(user));

            var currentRoles = await userManager.GetRolesAsync(user);
            var targetRoles = roles.Select(role => role.Name!).ToList();
            var remove = currentRoles.Except(targetRoles, StringComparer.OrdinalIgnoreCase).ToList();
            var add = targetRoles.Except(currentRoles, StringComparer.OrdinalIgnoreCase).ToList();
            if (remove.Count > 0) EnsureSucceeded(await userManager.RemoveFromRolesAsync(user, remove));
            if (add.Count > 0) EnsureSucceeded(await userManager.AddToRolesAsync(user, add));

            AddAudit(user.TenantKey, actorUserId, "ACCOUNT_UPDATED", user.Id, new
            {
                user.UserName,
                user.Email,
                user.DisplayName,
                Portal = user.Portal.ToString(),
                Roles = roles.Select(role => role.RoleCode),
                user.IsActive
            });
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
        return (await MapAsync([user], cancellationToken)).Single();
    }

    public async Task<AccessAccountDto> TransferPortalAsync(
        TransferAccountPortalRequest request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (request.Portal == AccountPortal.Platform)
        {
            throw new InvalidOperationException("Accounts cannot be moved into the platform portal.");
        }

        var user = await GetClientUserAsync(request.AccountId);
        var previousPortal = user.Portal;

        if (previousPortal == request.Portal)
        {
            throw new InvalidOperationException($"This account is already on the {request.Portal} portal.");
        }

        var roles = await ResolveRolesAsync(user.TenantKey!, request.Portal, request.RoleIds, cancellationToken);
        await EnsureEmployeeNumberAvailableAsync(user.TenantKey!, request.EmployeeNumber, user.Id, cancellationToken);

        var userName = request.UserName.Trim();
        var takenBy = await userManager.FindByNameAsync(userName);
        if (takenBy is not null && takenBy.Id != user.Id)
        {
            throw new InvalidOperationException($"Username '{userName}' is already in use.");
        }

        var executionStrategy = dbContext.Database.CreateExecutionStrategy();
        await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            user.Portal = request.Portal;
            user.UserName = userName;
            user.EmployeeNumber = NormalizeOptional(request.EmployeeNumber);

            // The account is no longer an applicant's, so it must stop minting the candidate
            // claim. The link from the person to their application survives on the employee
            // record, which is where it belongs.
            if (previousPortal == AccountPortal.Candidate)
            {
                user.CandidateId = null;
            }

            // The sign-in this records happened on the portal the account has just left, and
            // reading it as activity on the new one would tell an administrator the person is
            // already using an app they have never opened.
            user.LastLoginUtc = null;

            user.IsActive = true;

            // A portal transfer re-decides mobile access with the portal. An account moving from
            // Candidate to Employee is a different account in every way that matters, and its
            // old grant should not follow it across.
            user.MobileAccessEnabled = request.MobileAccessEnabled;
            user.UpdatedUtc = DateTimeOffset.UtcNow;
            EnsureSucceeded(await userManager.UpdateAsync(user));

            var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
            EnsureSucceeded(await userManager.ResetPasswordAsync(user, resetToken, request.TemporaryPassword));
            user.MustChangePassword = true;
            user.PasswordChangedUtc = null;
            EnsureSucceeded(await userManager.UpdateAsync(user));

            // Roles are portal-scoped, so what the old portal granted cannot simply carry over.
            var currentRoles = await userManager.GetRolesAsync(user);
            if (currentRoles.Count > 0)
            {
                EnsureSucceeded(await userManager.RemoveFromRolesAsync(user, currentRoles));
            }
            EnsureSucceeded(await userManager.AddToRolesAsync(user, roles.Select(role => role.Name!)));

            AddAudit(user.TenantKey!, actorUserId, "ACCOUNT_PORTAL_TRANSFERRED", user.Id, new
            {
                From = previousPortal.ToString(),
                To = request.Portal.ToString(),
                user.UserName,
                user.Email,
                user.EmployeeNumber,
                Roles = roles.Select(role => role.RoleCode)
            });

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

        return (await MapAsync([user], cancellationToken)).Single();
    }

    public async Task SetMobileAccessAsync(
        string accountId,
        bool enabled,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await GetClientUserAsync(accountId);
        user.MobileAccessEnabled = enabled;
        user.UpdatedUtc = DateTimeOffset.UtcNow;
        EnsureSucceeded(await userManager.UpdateAsync(user));

        // Audited under its own action, so "who gave this person a phone" is answerable
        // separately from "who enabled this account".
        AddAudit(user.TenantKey, actorUserId,
            enabled ? "MOBILE_ACCESS_GRANTED" : "MOBILE_ACCESS_REVOKED", user.Id,
            new { user.UserName, user.Portal, user.MobileAccessEnabled });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SetActiveAsync(
        string accountId,
        bool isActive,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await GetClientUserAsync(accountId);
        user.IsActive = isActive;
        user.UpdatedUtc = DateTimeOffset.UtcNow;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        AddAudit(user.TenantKey, actorUserId, isActive ? "ACCOUNT_ENABLED" : "ACCOUNT_DISABLED", user.Id,
            new { user.UserName, user.IsActive });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UnlockAsync(
        string accountId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await GetClientUserAsync(accountId);
        EnsureSucceeded(await userManager.SetLockoutEndDateAsync(user, null));
        EnsureSucceeded(await userManager.ResetAccessFailedCountAsync(user));
        user.UpdatedUtc = DateTimeOffset.UtcNow;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        AddAudit(user.TenantKey, actorUserId, "ACCOUNT_UNLOCKED", user.Id, new { user.UserName });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ResetPasswordAsync(
        ResetAccountPasswordRequest request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await GetClientUserAsync(request.AccountId);
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        EnsureSucceeded(await userManager.ResetPasswordAsync(user, token, request.TemporaryPassword));
        user.MustChangePassword = true;
        user.PasswordChangedUtc = null;
        user.UpdatedUtc = DateTimeOffset.UtcNow;
        EnsureSucceeded(await userManager.UpdateAsync(user));
        AddAudit(user.TenantKey, actorUserId, "PASSWORD_RESET", user.Id,
            new { user.UserName, MustChangePassword = true });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<List<HrRole>> ResolveRolesAsync(
        string tenantKey,
        AccountPortal portal,
        IEnumerable<string> roleIds,
        CancellationToken cancellationToken)
    {
        var distinctIds = roleIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            throw new InvalidOperationException("Select at least one role.");
        }

        var roles = await dbContext.Roles
            .Where(role => distinctIds.Contains(role.Id))
            .ToListAsync(cancellationToken);
        if (roles.Count != distinctIds.Count)
        {
            throw new InvalidOperationException("One or more selected roles no longer exist.");
        }
        if (roles.Any(role => role.TenantKey != tenantKey || role.Portal != portal))
        {
            throw new InvalidOperationException("Every assigned role must belong to the same client and portal as the account.");
        }
        if (roles.Any(role => !role.IsActive))
        {
            throw new InvalidOperationException("Inactive roles cannot be assigned to an account.");
        }
        return roles;
    }

    private async Task<IReadOnlyList<AccessAccountDto>> MapAsync(
        IReadOnlyList<AdminUser> users,
        CancellationToken cancellationToken)
    {
        var userIds = users.Select(user => user.Id).ToList();
        var assignments = await (
            from userRole in dbContext.UserRoles.AsNoTracking()
            join role in dbContext.Roles.AsNoTracking() on userRole.RoleId equals role.Id
            where userIds.Contains(userRole.UserId)
            select new { userRole.UserId, Role = role })
            .ToListAsync(cancellationToken);
        var assignmentMap = assignments.GroupBy(item => item.UserId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Role).ToList());
        var now = DateTimeOffset.UtcNow;

        return users.Select(user =>
        {
            var roles = assignmentMap.GetValueOrDefault(user.Id, []);
            return new AccessAccountDto
            {
                Id = user.Id,
                TenantKey = user.TenantKey ?? string.Empty,
                Portal = user.Portal,
                DisplayName = user.DisplayName,
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                EmployeeNumber = user.EmployeeNumber,
                IsActive = user.IsActive,
                MobileAccessEnabled = user.MobileAccessEnabled,
                MustChangePassword = user.MustChangePassword,
                IsLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > now,
                LockoutEnd = user.LockoutEnd,
                LastLoginUtc = user.LastLoginUtc,
                CreatedUtc = user.CreatedUtc,
                UpdatedUtc = user.UpdatedUtc,
                RoleIds = roles.Select(role => role.Id).ToList(),
                RoleNames = roles.Select(role => role.DisplayName).OrderBy(name => name).ToList()
            };
        }).ToList();
    }

    private async Task<AdminUser> GetClientUserAsync(string accountId)
    {
        var user = await userManager.FindByIdAsync(accountId)
            ?? throw new InvalidOperationException("Account was not found.");
        if (user.Portal == AccountPortal.Platform || string.IsNullOrWhiteSpace(user.TenantKey))
        {
            throw new InvalidOperationException("Platform administrator accounts cannot be changed here.");
        }
        return user;
    }

    private async Task EnsureEmailAvailableAsync(
        string email,
        string? exceptUserId,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = userManager.NormalizeEmail(email.Trim());
        if (await dbContext.Users.AnyAsync(user =>
            user.NormalizedEmail == normalizedEmail && user.Id != exceptUserId, cancellationToken))
        {
            throw new InvalidOperationException($"Email '{email.Trim()}' is already assigned to another account.");
        }
    }

    private async Task EnsureEmployeeNumberAvailableAsync(
        string tenantKey,
        string? employeeNumber,
        string? exceptUserId,
        CancellationToken cancellationToken)
    {
        employeeNumber = NormalizeOptional(employeeNumber);
        if (employeeNumber is not null && await dbContext.Users.AnyAsync(user =>
            user.TenantKey == tenantKey && user.EmployeeNumber == employeeNumber && user.Id != exceptUserId,
            cancellationToken))
        {
            throw new InvalidOperationException($"Employee number '{employeeNumber}' is already assigned in this client.");
        }
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
        string subjectId,
        object details) => dbContext.IdentityAudits.Add(new IdentityAuditEntity
    {
        TenantKey = tenantKey,
        ActorUserId = actorUserId,
        Action = action,
        SubjectType = "Account",
        SubjectId = subjectId,
        DetailJson = JsonSerializer.Serialize(details),
        OccurredUtc = DateTimeOffset.UtcNow
    });

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}
