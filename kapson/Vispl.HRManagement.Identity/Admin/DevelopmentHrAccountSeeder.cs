using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.Identity.Admin;

/// <summary>
/// Creates one HR account so the mobile app can be exercised locally.
///
/// It is opt-in and Development-only. Credentials are read from configuration and are never
/// defaulted, so nothing is seeded unless a developer deliberately supplies them — typically
/// as environment variables, which keeps them out of source and out of appsettings:
///
///   DevHrAccount__UserName, DevHrAccount__Password, DevHrAccount__TenantKey
///
/// The account is granted a role holding the recruitment and employee permissions the mobile
/// screens need. It is not a substitute for creating real accounts through /admin/accounts.
/// </summary>
public sealed class DevelopmentHrAccountSeeder(
    AdminIdentityDbContext dbContext,
    UserManager<AdminUser> userManager,
    RoleManager<HrRole> roleManager,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<DevelopmentHrAccountSeeder> logger)
{
    private const string RoleName = "DevHrReviewer";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        var userName = configuration["DevHrAccount:UserName"];
        var password = configuration["DevHrAccount:Password"];
        var tenantKey = configuration["DevHrAccount:TenantKey"];

        if (string.IsNullOrWhiteSpace(userName) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(tenantKey))
        {
            return;
        }

        // The role and its grants are ensured before the account check, so a permission added
        // to the catalogue since the role was created still reaches an existing dev account.
        var role = await roleManager.Roles
            .FirstOrDefaultAsync(item => item.Name == RoleName, cancellationToken);

        if (role is null)
        {
            role = new HrRole
            {
                Name = RoleName,
                RoleCode = "DEV_HR_REVIEWER",
                DisplayName = "Development HR Reviewer",
                Description = "Local development role for exercising the mobile HR screens.",
                Portal = AccountPortal.Hr,
                TenantKey = tenantKey,
                IsSystem = false,
                IsActive = true,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };

            var created = await roleManager.CreateAsync(role);
            if (!created.Succeeded)
            {
                logger.LogWarning("Could not create the development HR role: {Errors}",
                    string.Join("; ", created.Errors.Select(error => error.Description)));
                return;
            }

            var grants = HrPermissionCatalog.All
                .Select(permission => new RolePermissionEntity
                {
                    RoleId = role.Id,
                    PermissionKey = permission.Key,
                    CreatedUtc = DateTimeOffset.UtcNow
                });

            dbContext.RolePermissions.AddRange(grants);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // The role may predate a permission added to the catalogue since it was created, so
        // top up rather than leaving the dev account unable to reach a new endpoint.
        var granted = await dbContext.RolePermissions
            .Where(item => item.RoleId == role.Id)
            .Select(item => item.PermissionKey)
            .ToListAsync(cancellationToken);

        var missing = HrPermissionCatalog.All
            .Where(permission => !granted.Contains(permission.Key))
            .Select(permission => new RolePermissionEntity
            {
                RoleId = role.Id,
                PermissionKey = permission.Key,
                CreatedUtc = DateTimeOffset.UtcNow
            })
            .ToList();

        if (missing.Count > 0)
        {
            dbContext.RolePermissions.AddRange(missing);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogWarning(
                "Granted {Count} newly catalogued permission(s) to the development HR role.", missing.Count);
        }

        var existing = await userManager.FindByNameAsync(userName);
        if (existing is not null)
        {
            // Accounts seeded before the mobile access grant existed were left without it, and
            // the token endpoint refuses staff without the grant — so repair rather than skip.
            if (!existing.MobileAccessEnabled || string.IsNullOrWhiteSpace(existing.EmployeeNumber))
            {
                existing.MobileAccessEnabled = true;
                existing.EmployeeNumber ??= "EMP-1001";
                existing.UpdatedUtc = DateTimeOffset.UtcNow;
                await userManager.UpdateAsync(existing);
                logger.LogWarning("Enabled mobile access / employee number for development HR account '{UserName}'.", userName);
            }

            return;
        }

        var user = new AdminUser
        {
            UserName = userName,
            Email = $"{userName}@localhost",
            DisplayName = "Development HR",
            TenantKey = tenantKey,
            Portal = AccountPortal.Hr,
            EmployeeNumber = "EMP-1001",
            IsActive = true,
            // The mobile token endpoint refuses staff accounts without this grant.
            MobileAccessEnabled = true,
            // False so the token flow can be exercised without a password-change round trip.
            // Real accounts created through /admin/accounts keep the forced change.
            MustChangePassword = false,
            CreatedUtc = DateTimeOffset.UtcNow,
            UpdatedUtc = DateTimeOffset.UtcNow
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            logger.LogWarning("Could not create the development HR account: {Errors}",
                string.Join("; ", result.Errors.Select(error => error.Description)));
            return;
        }

        await userManager.AddToRoleAsync(user, RoleName);

        logger.LogWarning(
            "Development HR account '{UserName}' created for tenant '{TenantKey}'. " +
            "This runs only in Development and only when DevHrAccount credentials are configured.",
            userName, tenantKey);
    }
}
