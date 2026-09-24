using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.Identity.Admin;

public sealed class AdminIdentityInitializer(
    AdminIdentityDbContext dbContext,
    UserManager<AdminUser> userManager,
    RoleManager<HrRole> roleManager,
    ITenantService tenantService,
    IAccessRoleService accessRoleService,
    IConfiguration configuration,
    ILogger<AdminIdentityInitializer> logger)
{
    public const string AdministratorRole = "VertexAdministrator";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Applying Admin Identity SQL Server migrations.");
        await dbContext.Database.MigrateAsync(cancellationToken);

        if (!await roleManager.RoleExistsAsync(AdministratorRole))
        {
            EnsureSucceeded(await roleManager.CreateAsync(new HrRole
            {
                Name = AdministratorRole,
                RoleCode = "VERTEX_ADMINISTRATOR",
                DisplayName = "Vertex Administrator",
                Description = "Protected platform administrator with access to every licensed client.",
                Portal = AccountPortal.Platform,
                IsSystem = true,
                IsActive = true,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            }));
        }

        var userName = configuration["BootstrapAdmin:UserName"] ?? "admin";
        if (await userManager.FindByNameAsync(userName) is null)
        {
            var initialPassword = configuration["BootstrapAdmin:Password"];
            if (string.IsNullOrWhiteSpace(initialPassword))
            {
                throw new InvalidOperationException(
                    "BootstrapAdmin:Password is required only when the first administrator account does not exist. " +
                    "Set BootstrapAdmin__Password in the deployment environment.");
            }

            var user = new AdminUser
            {
                UserName = userName,
                DisplayName = configuration["BootstrapAdmin:DisplayName"] ?? "Vertex Administrator",
                EmailConfirmed = true,
                Portal = AccountPortal.Platform,
                IsActive = true,
                MustChangePassword = true,
                CreatedUtc = DateTimeOffset.UtcNow,
                UpdatedUtc = DateTimeOffset.UtcNow
            };

            EnsureSucceeded(await userManager.CreateAsync(user, initialPassword));
            EnsureSucceeded(await userManager.AddToRoleAsync(user, AdministratorRole));
            logger.LogWarning("Created the bootstrap administrator '{UserName}'. A password change is required at first login.", userName);
        }

        var activeTenants = (await tenantService.GetAllAsync(cancellationToken))
            .Where(tenant => tenant.IsActive);
        foreach (var tenant in activeTenants)
        {
            await accessRoleService.GetAllAsync(tenant.TenantKey, AccountPortal.Hr, cancellationToken);
            await accessRoleService.GetAllAsync(tenant.TenantKey, AccountPortal.Employee, cancellationToken);
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(string.Join(" ", result.Errors.Select(error => error.Description)));
        }
    }
}
