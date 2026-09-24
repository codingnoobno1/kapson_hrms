using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Composition.Shared;
using Vispl.HRManagement.Contracts.Mobile.Employee;

// A one-shot admin utility: creates an employee record and grants them employee-app access,
// entirely through the same business services the HR portal and mobile API use — no HTTP, no
// sign-in. Run it whenever a new employee needs onboarding onto the app without going through
// the HR portal UI by hand. The temporary password is printed once and never stored anywhere,
// matching what IEmployeeMobileAccessService.GrantAsync itself guarantees.

var options = CommandLineOptions.Parse(args);

if (options is null)
{
    return 1;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:HrManagement"] =
        "Server=localhost\\SQLEXPRESS;Database=KapsonHRManagement;Integrated Security=True;" +
        "Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=True"
});

builder.Services
    .AddDataProtection()
    .SetApplicationName("Vispl.HRManagement")
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
            "Vispl.HRManagement.Host", ".data-protection-keys")));

builder.Services.AddHrManagement(builder.Configuration);

using var host = builder.Build();

var employees = host.Services.GetRequiredService<IMobileEmployeeService>();
var access = host.Services.GetRequiredService<IEmployeeMobileAccessService>();

var caller = new MobileCallerContext(
    TenantKey: options.TenantKey,
    UserId: "onboard-employee-tool",
    EmployeeId: null,
    Permissions: new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "EMPLOYEES.CREATE",
        "EMPLOYEES.VIEW",
        "ACCOUNTS.MANAGE"
    },
    CorrelationId: Guid.NewGuid().ToString("n"));

try
{
    var created = await employees.CreateAsync(caller, new CreateEmployeeRequest
    {
        LegalName = options.Name,
        Email = options.Email,
        EmployeeNumber = options.EmployeeNumber,
        JoinedOn = options.JoinedOn,
        DepartmentCode = options.DepartmentCode,
        LocationCode = options.LocationCode,
        JobTitle = options.JobTitle,
        Phone = options.Phone
    }, CancellationToken.None);

    Console.WriteLine($"Employee created: {options.Name} ({options.EmployeeNumber}), id {created.EmployeeId}");

    var granted = await access.GrantAsync(caller, created.EmployeeId, new GrantMobileAccessRequest(), CancellationToken.None);

    Console.WriteLine();
    Console.WriteLine("Employee app sign-in issued — shown once, save it now:");
    Console.WriteLine($"  User name          : {granted.UserName}");
    Console.WriteLine($"  Temporary password : {granted.TemporaryPassword}");
    Console.WriteLine($"  Role               : {granted.RoleName}");
    Console.WriteLine("  They must change this password at first sign-in.");

    return 0;
}
catch (MobileValidationException ex)
{
    Console.Error.WriteLine($"Rejected ({ex.Code}): {ex.Message}");
    return 1;
}
catch (MobileAccessDeniedException ex)
{
    Console.Error.WriteLine($"Denied: needs {ex.Permission}");
    return 1;
}

sealed record CommandLineOptions(
    string Name,
    string Email,
    string EmployeeNumber,
    DateOnly JoinedOn,
    string DepartmentCode,
    string LocationCode,
    string? JobTitle,
    string? Phone,
    string TenantKey)
{
    public static CommandLineOptions? Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < args.Length - 1; i += 2)
        {
            map[args[i].TrimStart('-')] = args[i + 1];
        }

        if (!map.TryGetValue("name", out var name) || !map.TryGetValue("email", out var email))
        {
            Console.Error.WriteLine(
                "Usage: dotnet run -- --name \"Full Name\" --email a@b.com [--employee-number EMP-10046] " +
                "[--department RETAIL] [--location CHANDIGARH] [--job-title \"Store Associate\"] " +
                "[--phone 9999999999] [--tenant kapson]");
            return null;
        }

        var employeeNumber = map.GetValueOrDefault("employee-number") ??
            $"EMP-{Random.Shared.Next(20000, 29999)}";

        return new CommandLineOptions(
            Name: name,
            Email: email,
            EmployeeNumber: employeeNumber,
            JoinedOn: DateOnly.FromDateTime(DateTime.Today),
            DepartmentCode: map.GetValueOrDefault("department", "RETAIL"),
            LocationCode: map.GetValueOrDefault("location", "CHANDIGARH"),
            JobTitle: map.GetValueOrDefault("job-title"),
            Phone: map.GetValueOrDefault("phone"),
            TenantKey: map.GetValueOrDefault("tenant", "kapson"));
    }
}
