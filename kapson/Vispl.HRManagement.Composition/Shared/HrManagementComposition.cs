using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vispl.HRManagement.Composition.Admin;
using Vispl.HRManagement.Composition.Candidate;
using Vispl.HRManagement.Composition.Employee;
using Vispl.HRManagement.Composition.Recruitment;
using Vispl.HRManagement.BM.Shared;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.Identity.Shared;
using Vispl.HRManagement.Identity.Admin;
using Vispl.HRManagement.Worker.Shared;
using Vispl.HRManagement.Mail.Shared;

namespace Vispl.HRManagement.Composition.Shared;

public static class HrManagementComposition
{
    public static IServiceCollection AddHrManagement(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHrManagementData(configuration);
        services.AddHrManagementBusiness();
        services.AddSingleton<BM.ITF.Shared.ISensitiveValueProtector, DataProtectionSensitiveValueProtector>();
        services.AddHrIdentity(configuration);
        services.AddAdminModule(configuration);
        services.AddRecruitmentModule(configuration);
        services.AddCandidateModule(configuration);
        services.AddEmployeeModule(configuration);
        services.AddHrMail(configuration);
        services.AddHrWorkers(configuration);
        return services;
    }

    public static IEndpointRouteBuilder MapHrManagement(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHrIdentityEndpoints();
        endpoints.MapAdmin();
        endpoints.MapRecruitment();
        endpoints.MapCandidate();
        endpoints.MapEmployee();
        return endpoints;
    }

    public static async Task InitializeHrManagementAsync(
        this WebApplication application,
        CancellationToken cancellationToken = default)
    {
        if (!application.Configuration.GetValue("Database:InitializeOnStartup", true))
        {
            return;
        }

        var configuredPath = application.Configuration["Database:SeedDataPath"] ?? "SeedData";
        var seedDataPath = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(application.Environment.ContentRootPath, configuredPath);

        await using var scope = application.Services.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<HrManagementDatabaseInitializer>();
        await initializer.InitializeAsync(
            seedDataPath,
            application.Configuration.GetValue("Database:SeedOnEmpty", true),
            cancellationToken);

        var identityInitializer = scope.ServiceProvider.GetRequiredService<AdminIdentityInitializer>();
        await identityInitializer.InitializeAsync(cancellationToken);

        // Opt-in, Development-only, and a no-op unless DevHrAccount credentials are configured.
        var developmentHrSeeder = scope.ServiceProvider.GetRequiredService<DevelopmentHrAccountSeeder>();
        await developmentHrSeeder.SeedAsync(cancellationToken);

        await LinkHrAccountsToEmployeesAsync(scope.ServiceProvider, application.Environment.IsDevelopment(), cancellationToken);
    }

    private static async Task LinkHrAccountsToEmployeesAsync(
        IServiceProvider services,
        bool isDevelopment,
        CancellationToken cancellationToken)
    {
        var identityDb = services.GetRequiredService<AdminIdentityDbContext>();
        var hrDb = services.GetRequiredService<HrManagementDbContext>();
        var userManager = services.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AdminUser>>();

        var employees = await hrDb.Employees
            .Where(e => e.IsActive)
            .ToListAsync(cancellationToken);

        if (employees.Count == 0)
        {
            return;
        }

        if (isDevelopment)
        {
            const string tenantKey = "kapson";
            var hasEmployeeAccounts = await identityDb.Users.AnyAsync(
                u => u.TenantKey == tenantKey && u.Portal == Vispl.HRManagement.Contracts.Admin.AccountPortal.Employee,
                cancellationToken);

            if (!hasEmployeeAccounts)
            {
                var sampleAccounts = new (string UserName, string EmpNum, bool IsManager, bool MobileEnabled)[]
                {
                    ("mgr.vikram", "EMP-1004", true, true),
                    ("mgr.kabir", "EMP-1007", true, true),
                    ("mgr.aditya", "EMP-1011", true, true),
                    ("mgr.gurpreet", "EMP-1015", true, true),
                    ("emp.rohan", "EMP-1002", false, true),
                    ("emp.ananya", "EMP-1008", false, true),
                    ("emp.manpreet", "EMP-1018", false, false),
                    ("emp.siddharth", "EMP-1024", false, false)
                };

                var now = DateTimeOffset.UtcNow;
                foreach (var acc in sampleAccounts)
                {
                    if (await userManager.FindByNameAsync(acc.UserName) is not null)
                    {
                        continue;
                    }

                    var emp = employees.FirstOrDefault(e =>
                        string.Equals(e.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(e.EmployeeNumber, acc.EmpNum, StringComparison.OrdinalIgnoreCase));

                    if (emp is null)
                    {
                        continue;
                    }

                    var newUser = new AdminUser
                    {
                        UserName = acc.UserName,
                        Email = emp.Email ?? $"{acc.UserName}@kapson.in",
                        EmailConfirmed = true,
                        DisplayName = $"{emp.LegalFirstName} {emp.LegalLastName}".Trim(),
                        TenantKey = tenantKey,
                        Portal = Vispl.HRManagement.Contracts.Admin.AccountPortal.Employee,
                        EmployeeNumber = emp.EmployeeNumber,
                        IsActive = true,
                        MobileAccessEnabled = acc.MobileEnabled,
                        MustChangePassword = false,
                        LockoutEnabled = true,
                        CreatedUtc = now.AddDays(-20),
                        UpdatedUtc = now
                    };

                    var created = await userManager.CreateAsync(newUser, "EmpDev@2026!");
                    if (created.Succeeded)
                    {
                        await userManager.AddToRoleAsync(newUser, "TENANT::KAPSON::EMPLOYEE::EMPLOYEE");
                        if (acc.IsManager)
                        {
                            await userManager.AddToRoleAsync(newUser, "TENANT::KAPSON::EMPLOYEE::REPORTING_MANAGER");
                        }
                    }
                }
            }
        }

        var tenantUsers = await identityDb.Users
            .Where(u => u.TenantKey != null && u.IsActive)
            .ToListAsync(cancellationToken);

        if (tenantUsers.Count == 0)
        {
            return;
        }

        var identityChanged = false;
        var employeesChanged = false;

        if (isDevelopment)
        {
            var assignedNumbers = tenantUsers
                .Where(u => !string.IsNullOrWhiteSpace(u.EmployeeNumber))
                .Select(u => u.EmployeeNumber!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var hrUser in tenantUsers.Where(u =>
                         u.Portal == Vispl.HRManagement.Contracts.Admin.AccountPortal.Hr &&
                         string.IsNullOrWhiteSpace(u.EmployeeNumber)))
            {
                var candidateEmp = employees
                    .FirstOrDefault(e =>
                        string.Equals(e.TenantKey, hrUser.TenantKey, StringComparison.OrdinalIgnoreCase) &&
                        string.Equals(e.DepartmentCode, "HR", StringComparison.OrdinalIgnoreCase) &&
                        !assignedNumbers.Contains(e.EmployeeNumber))
                    ?? employees.FirstOrDefault(e =>
                        string.Equals(e.TenantKey, hrUser.TenantKey, StringComparison.OrdinalIgnoreCase) &&
                        !assignedNumbers.Contains(e.EmployeeNumber));

                if (candidateEmp is not null)
                {
                    hrUser.EmployeeNumber = candidateEmp.EmployeeNumber;
                    assignedNumbers.Add(candidateEmp.EmployeeNumber);
                    identityChanged = true;
                }
            }
        }

        if (identityChanged)
        {
            await identityDb.SaveChangesAsync(cancellationToken);
        }

        foreach (var user in tenantUsers.Where(u => !string.IsNullOrWhiteSpace(u.EmployeeNumber)))
        {
            var emp = employees.FirstOrDefault(e =>
                string.Equals(e.TenantKey, user.TenantKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(e.EmployeeNumber, user.EmployeeNumber, StringComparison.OrdinalIgnoreCase));

            if (emp is not null && !string.Equals(emp.AccountId, user.Id, StringComparison.Ordinal))
            {
                emp.AccountId = user.Id;
                employeesChanged = true;
            }
        }

        if (employeesChanged)
        {
            await hrDb.SaveChangesAsync(cancellationToken);
        }

        if (isDevelopment)
        {
            var hrLeadAccountId = employees
                .FirstOrDefault(e => e.TenantKey == "kapson" && e.EmployeeNumber == "EMP-1001")
                ?.AccountId;

            if (!string.IsNullOrWhiteSpace(hrLeadAccountId))
            {
                foreach (var p in await hrDb.InterviewPanelists
                             .Where(p => p.TenantKey == "kapson" && p.PanelMemberId == "hrdev")
                             .ToListAsync(cancellationToken))
                {
                    p.PanelMemberId = hrLeadAccountId;
                }

                foreach (var ev in await hrDb.InterviewEvaluations
                             .Where(ev => ev.TenantKey == "kapson" && ev.PanelMemberId == "hrdev")
                             .ToListAsync(cancellationToken))
                {
                    ev.PanelMemberId = hrLeadAccountId;
                }

                await hrDb.SaveChangesAsync(cancellationToken);
            }

            if (!await hrDb.Notifications.AnyAsync(n => n.TenantKey == "kapson" && n.Category == "Interview", cancellationToken))
            {
                var now = DateTimeOffset.UtcNow;
                var notifications = new List<NotificationEntity>();

                if (!string.IsNullOrWhiteSpace(hrLeadAccountId))
                {
                    notifications.AddRange(
                        new NotificationEntity
                        {
                            TenantKey = "kapson",
                            RecipientId = hrLeadAccountId,
                            Category = "Leave",
                            Title = "6 leave requests awaiting approval",
                            Body = "Neha Sharma, Devansh Khanna, Harleen Sidhu and 3 others have pending leave requests in the queue.",
                            Priority = "High",
                            ActionRoute = "/leave/team/requests",
                            SubjectType = "LeaveQueue",
                            SubjectId = "pending-batch",
                            OccurredUtc = now.AddHours(-2)
                        },
                        new NotificationEntity
                        {
                            TenantKey = "kapson",
                            RecipientId = hrLeadAccountId,
                            Category = "Interview",
                            Title = "Interview scheduled today at 11:00 UTC",
                            Body = "Round 2 — Technical & Domain Review is scheduled online. Tap to open the panel brief and meeting link.",
                            Priority = "High",
                            ActionRoute = "/interviews",
                            SubjectType = "Interview",
                            SubjectId = "1",
                            OccurredUtc = now.AddHours(-4)
                        },
                        new NotificationEntity
                        {
                            TenantKey = "kapson",
                            RecipientId = hrLeadAccountId,
                            Category = "Interview",
                            Title = "Upcoming HR & Culture Fit Round tomorrow",
                            Body = "Round 1 — HR & Culture Fit is scheduled for tomorrow at 10:00 UTC.",
                            Priority = "Normal",
                            ActionRoute = "/interviews",
                            SubjectType = "Interview",
                            SubjectId = "2",
                            OccurredUtc = now.AddHours(-8)
                        },
                        new NotificationEntity
                        {
                            TenantKey = "kapson",
                            RecipientId = hrLeadAccountId,
                            Category = "Onboarding",
                            Title = "Siddharth Oberoi submitted Emergency Contact Form",
                            Body = "Mid-month joiner EMP-1024 submitted onboarding task #4 for HR verification.",
                            Priority = "Normal",
                            ActionRoute = "/onboarding",
                            SubjectType = "Onboarding",
                            SubjectId = "EMP-1024",
                            OccurredUtc = now.AddDays(-1)
                        },
                        new NotificationEntity
                        {
                            TenantKey = "kapson",
                            RecipientId = hrLeadAccountId,
                            Category = "Payroll",
                            Title = "September monthly payroll disbursed",
                            Body = "NEFT batch for 23 employees has been reconciled and payslips are now published to the mobile app.",
                            Priority = "Normal",
                            ActionRoute = "/payroll/payslips",
                            SubjectType = "PayrollRun",
                            SubjectId = "3",
                            OccurredUtc = now.AddDays(-12),
                            ReadUtc = now.AddDays(-11)
                        });
                }

                foreach (var empWithAccount in employees.Where(e =>
                             e.TenantKey == "kapson" &&
                             !string.IsNullOrWhiteSpace(e.AccountId) &&
                             e.EmployeeNumber != "EMP-1001"))
                {
                    notifications.AddRange(
                        new NotificationEntity
                        {
                            TenantKey = "kapson",
                            RecipientId = empWithAccount.AccountId!,
                            Category = "Payroll",
                            Title = "Your latest monthly payslip is ready",
                            Body = "Your approved monthly payslip has been published. Tap to view earnings and deductions.",
                            Priority = "Normal",
                            ActionRoute = "/payroll/payslips",
                            SubjectType = "Payslip",
                            SubjectId = empWithAccount.EmployeeNumber,
                            OccurredUtc = now.AddDays(-10)
                        },
                        new NotificationEntity
                        {
                            TenantKey = "kapson",
                            RecipientId = empWithAccount.AccountId!,
                            Category = "System",
                            Title = "Updated company policies available",
                            Body = "Please review and acknowledge the published Attendance and Work From Home policies.",
                            Priority = "Normal",
                            ActionRoute = "/policies",
                            SubjectType = "Policy",
                            SubjectId = "ATTENDANCE_POLICY",
                            OccurredUtc = now.AddDays(-5)
                        });
                }

                if (notifications.Count > 0)
                {
                    hrDb.Notifications.AddRange(notifications);
                    await hrDb.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }
}
