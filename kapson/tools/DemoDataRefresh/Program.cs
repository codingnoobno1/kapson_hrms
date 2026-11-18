using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.Composition.Shared;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Identity.Candidate;
using DemoDataRefresh;

// Second half of the demo data refresh (run demo-refresh.sql first). Everything here goes
// through the application's own services — the same code the HR portal runs — so logins,
// encrypted bank numbers and payslips are exactly what the app would have produced itself.
//
//   dotnet run -- --step seed     logins, bank accounts, payroll Jul–Sep, notifications
//   dotnet run -- --step report   HTML + PDF of the dataset, including the temporary passwords
//   dotnet run                    both

var options = ToolOptions.Parse(args);
Directory.CreateDirectory(options.OutputDirectory);
var credentialsPath = Path.Combine(options.OutputDirectory, "credentials.json");

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = [],
    EnvironmentName = Environments.Production
});

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o => o.SingleLine = true);
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Error);

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["ConnectionStrings:HrManagement"] = options.ConnectionString,
    ["Mail:Enabled"] = "false",
    // Never used to sign anything here; present only because the provider insists outside Development.
    ["MobileApi:SigningKey"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
});

// The live site's key ring, read-only: bank numbers encrypted here must be readable by the site.
builder.Services
    .AddDataProtection()
    .SetApplicationName("Vispl.HRManagement")
    .PersistKeysToFileSystem(new DirectoryInfo(options.KeysPath))
    .DisableAutomaticKeyGeneration();

builder.Services.AddHrManagement(builder.Configuration);

using var host = builder.Build();

if (options.Step is "all" or "seed")
{
    await using var scope = host.Services.CreateAsyncScope();
    var seeder = new Seeder(scope.ServiceProvider, options);
    var credentials = await seeder.RunAsync();
    await File.WriteAllTextAsync(credentialsPath, JsonSerializer.Serialize(credentials, Json.Options));
    Console.WriteLine($"Credentials written to {credentialsPath}");
}

if (options.Step is "all" or "report")
{
    var credentials = JsonSerializer.Deserialize<List<Credential>>(await File.ReadAllTextAsync(credentialsPath), Json.Options) ?? [];
    var report = new ReportBuilder(options);
    var html = await report.BuildAsync(credentials);
    var htmlPath = Path.Combine(options.OutputDirectory, "Kapson-HRMS-Demo-Data.html");
    var pdfPath = Path.Combine(options.OutputDirectory, "Kapson-HRMS-Demo-Data.pdf");
    await File.WriteAllTextAsync(htmlPath, html, new UTF8Encoding(false));
    PdfPrinter.Print(htmlPath, pdfPath);
    Console.WriteLine($"Report written to {pdfPath}");
}

return 0;

namespace DemoDataRefresh
{
    sealed record ToolOptions(string Step, string ConnectionString, string KeysPath, string OutputDirectory, string Tenant, string SiteUrl)
    {
        public static ToolOptions Parse(string[] args)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < args.Length - 1; i += 2)
            {
                map[args[i].TrimStart('-')] = args[i + 1];
            }

            return new ToolOptions(
                map.GetValueOrDefault("step", "all").ToLowerInvariant(),
                map.GetValueOrDefault("connection",
                    "Server=localhost;Database=VisplHRManagement;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True"),
                map.GetValueOrDefault("keys", @"C:\inetpub\HRMS\.data-protection-keys"),
                map.GetValueOrDefault("out", Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "demo-data"))),
                map.GetValueOrDefault("tenant", "kapson"),
                map.GetValueOrDefault("site", "https://maritime-recognized-teachers-browser.trycloudflare.com"));
        }
    }

    sealed record Credential(string Portal, string EmployeeNumber, string Name, string Role, string UserName, string Password, string SignInAt);

    static class Json
    {
        public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    }

    static class Passwords
    {
        // Same shape as the application's own temporary passwords: 14 characters, every class present.
        public static string New()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789", symbols = "!#$%&*+-";
            var pool = upper + lower + digits + symbols;
            var chars = new List<char>
            {
                upper[RandomNumberGenerator.GetInt32(upper.Length)], lower[RandomNumberGenerator.GetInt32(lower.Length)],
                digits[RandomNumberGenerator.GetInt32(digits.Length)], symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
            };
            while (chars.Count < 14) chars.Add(pool[RandomNumberGenerator.GetInt32(pool.Length)]);
            for (var i = chars.Count - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }
            return new string([.. chars]);
        }
    }

    sealed record Person(long Id, string Number, string Name, string Email, string Title, string Department, long? ManagerId);

    sealed class Seeder(IServiceProvider services, ToolOptions options)
    {
        private const string Actor = "demo-data-refresh";
        private readonly IAccessAccountService accounts = services.GetRequiredService<IAccessAccountService>();
        private readonly IEmployeeMobileAccessService mobileAccess = services.GetRequiredService<IEmployeeMobileAccessService>();
        private readonly IBankAccountService banks = services.GetRequiredService<IBankAccountService>();
        private readonly IPayrollService payroll = services.GetRequiredService<IPayrollService>();
        private readonly INotificationPublisher notifications = services.GetRequiredService<INotificationPublisher>();
        private readonly ICandidateActivationService activation = services.GetRequiredService<ICandidateActivationService>();

        // HR team: employee number → (HR role code, username)
        private static readonly Dictionary<string, (string Role, string UserName)> HrTeam = new()
        {
            ["EMP-1001"] = ("HR_ADMINISTRATOR", "simran.kaur"),
            ["EMP-1002"] = ("HR_EXECUTIVE", "rohan.verma"),
            ["EMP-1023"] = ("RECRUITER", "rajvir.randhawa")
        };

        private MobileCallerContext Caller(string userId) => new(
            options.Tenant, userId, null,
            HrPermissionCatalog.All.Select(p => p.Key).Distinct().ToHashSet(StringComparer.OrdinalIgnoreCase),
            Guid.NewGuid().ToString("n"));

        public async Task<List<Credential>> RunAsync()
        {
            var credentials = new List<Credential>();
            var people = await LoadPeopleAsync();
            var hrUrl = $"{options.SiteUrl}/hr";

            Console.WriteLine("1/6 Re-encrypting bank accounts…");
            await SaveBankAccountsAsync(people);

            Console.WriteLine("2/6 HR portal accounts…");
            var roleIds = await QueryAsync("SELECT RoleCode, Id FROM [identity].Roles", r => (r.GetString(0), r.GetString(1)));
            var role = roleIds.ToDictionary(x => x.Item1, x => x.Item2, StringComparer.OrdinalIgnoreCase);
            var hrAccounts = (await accounts.GetAllAsync(options.Tenant, AccountPortal.Hr)).ToList();
            var accountIdByNumber = new Dictionary<string, string>();

            foreach (var person in people.Where(p => HrTeam.ContainsKey(p.Number)))
            {
                var (roleCode, userName) = HrTeam[person.Number];
                var password = Passwords.New();
                var existing = hrAccounts.FirstOrDefault(a => a.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
                string accountId;
                if (existing is null)
                {
                    var created = await accounts.CreateAsync(new CreateAccessAccountRequest
                    {
                        TenantKey = options.Tenant, Portal = AccountPortal.Hr, DisplayName = person.Name, UserName = userName,
                        Email = person.Email, EmployeeNumber = person.Number, TemporaryPassword = password,
                        IsActive = true, MobileAccessEnabled = true, RoleIds = [role[roleCode]]
                    }, Actor);
                    accountId = created.Id;
                }
                else
                {
                    accountId = existing.Id;
                    await accounts.ResetPasswordAsync(new ResetAccountPasswordRequest { AccountId = accountId, TemporaryPassword = password }, Actor);
                }

                accountIdByNumber[person.Number] = accountId;
                await ExecAsync("UPDATE employee.Employees SET AccountId = @a WHERE Id = @e", ("@a", accountId), ("@e", person.Id));
                credentials.Add(new Credential("HR portal", person.Number, person.Name, RoleLabel(roleCode), userName, password, hrUrl));
            }

            // Interviews and evaluations were attributed to the development login.
            var simranId = accountIdByNumber["EMP-1001"];
            var rajvirId = accountIdByNumber["EMP-1023"];
            await ExecAsync(@"
                DECLARE @dev nvarchar(450) = (SELECT Id FROM [identity].Users WHERE UserName = 'hrdev');
                UPDATE recruitment.InterviewPanelists  SET PanelMemberId = @s WHERE PanelMemberId = @dev;
                UPDATE recruitment.InterviewEvaluations SET PanelMemberId = @s WHERE PanelMemberId = @dev;
                INSERT recruitment.InterviewPanelists (TenantKey, InterviewId, PanelMemberId, DisplayName, IsLead, AddedUtc)
                SELECT i.TenantKey, i.Id, @r, 'Rajvir Randhawa', 0, SYSDATETIMEOFFSET()
                FROM recruitment.Interviews i
                WHERE i.Status = 'Scheduled' AND NOT EXISTS
                    (SELECT 1 FROM recruitment.InterviewPanelists p WHERE p.InterviewId = i.Id AND p.PanelMemberId = @r);",
                ("@s", simranId), ("@r", rajvirId));

            Console.WriteLine("3/6 Employee app accounts…");
            var managerIds = people.Where(p => p.ManagerId is not null).Select(p => p.ManagerId!.Value).ToHashSet();
            var hrCaller = Caller(simranId);
            var employeeAccounts = (await accounts.GetAllAsync(options.Tenant, AccountPortal.Employee)).ToList();

            foreach (var person in people.Where(p => !HrTeam.ContainsKey(p.Number)))
            {
                var existing = employeeAccounts.FirstOrDefault(a => string.Equals(a.EmployeeNumber, person.Number, StringComparison.OrdinalIgnoreCase));
                string userName, password;
                if (existing is null)
                {
                    var granted = await mobileAccess.GrantAsync(hrCaller, person.Id.ToString(), new GrantMobileAccessRequest(), CancellationToken.None);
                    (userName, password) = (granted.UserName, granted.TemporaryPassword);
                }
                else
                {
                    password = Passwords.New();
                    userName = existing.UserName;
                    await accounts.ResetPasswordAsync(new ResetAccountPasswordRequest { AccountId = existing.Id, TemporaryPassword = password }, Actor);
                }

                var account = (await accounts.GetAllAsync(options.Tenant, AccountPortal.Employee))
                    .First(a => a.UserName.Equals(userName, StringComparison.OrdinalIgnoreCase));
                accountIdByNumber[person.Number] = account.Id;

                var isManager = managerIds.Contains(person.Id);
                if (isManager && !account.RoleIds.Contains(role["REPORTING_MANAGER"]))
                {
                    await accounts.UpdateAsync(new UpdateAccessAccountRequest
                    {
                        Id = account.Id, DisplayName = account.DisplayName, Email = account.Email, EmployeeNumber = account.EmployeeNumber,
                        IsActive = true, MobileAccessEnabled = true, RoleIds = [role["EMPLOYEE"], role["REPORTING_MANAGER"]]
                    }, Actor);
                }

                credentials.Add(new Credential("Employee app", person.Number, person.Name,
                    isManager ? "Employee + Reporting Manager" : "Employee", userName, password, "Kapsons mobile app"));
            }

            Console.WriteLine("4/6 Candidate accounts…");
            foreach (var (candidateId, label) in new[] { (4L, "Candidate — onboarding (Store Manager)"), (10L, "Candidate — offer to accept (HR Executive)") })
            {
                var row = (await QueryAsync("SELECT Email, FullName, AccountId FROM recruitment.Candidates WHERE Id = @c",
                    r => (Email: r.GetString(0), Name: r.GetString(1), AccountId: r.IsDBNull(2) ? null : r.GetString(2)), ("@c", candidateId))).Single();
                var password = Passwords.New();
                if (row.AccountId is null)
                {
                    var token = await activation.RequestAsync(options.Tenant, row.Email)
                                ?? throw new InvalidOperationException($"No activation issued for {row.Email}.");
                    var result = await activation.ActivateAsync(options.Tenant, token, password);
                    if (!result.Succeeded) throw new InvalidOperationException(string.Join(" ", result.Errors));
                }
                else
                {
                    await accounts.ResetPasswordAsync(new ResetAccountPasswordRequest { AccountId = row.AccountId, TemporaryPassword = password }, Actor);
                }

                credentials.Add(new Credential("Candidate portal", "—", row.Name, label, row.Email, password, $"{options.SiteUrl}/onboarding"));
            }

            Console.WriteLine("5/6 Payroll July–September (generate → approve → bank transfer)…");
            var payrollCaller = Caller("Simran Kaur");
            foreach (var month in new[] { 7, 8, 9 })
            {
                await RunPayrollMonthAsync(payrollCaller, 2026, month);
            }

            Console.WriteLine("6/6 Notifications…");
            await PublishNotificationsAsync(accountIdByNumber, people);

            return credentials;
        }

        private async Task RunPayrollMonthAsync(MobileCallerContext caller, int year, int month)
        {
            var scope = new PayrollScopeQuery { Year = year, Month = month, SalaryType = PayrollSalaryTypes.Monthly };
            var existingRun = (await QueryAsync("SELECT Id, ApprovedUtc FROM payroll.PayrollRuns WHERE TenantKey = @t AND Year = @y AND Month = @m",
                r => (Id: r.GetInt64(0), Approved: !r.IsDBNull(1)), ("@t", options.Tenant), ("@y", year), ("@m", month))).SingleOrDefault();
            if (existingRun.Approved)
            {
                Console.WriteLine($"   {year}-{month:00}: already approved, skipped");
                return;
            }

            var generated = await payroll.GenerateAsync(caller, new GeneratePayrollRequest { Scope = scope });
            foreach (var reason in generated.SkippedReasons) Console.WriteLine($"   skipped: {reason}");
            await payroll.ApproveAsync(caller, scope);

            var runId = long.Parse(generated.RunId);
            var ids = await QueryAsync("SELECT EmployeeId FROM payroll.PayrollLines WHERE RunId = @r", r => r.GetInt64(0).ToString(), ("@r", runId));
            var next = new DateOnly(year, month, 1).AddMonths(1);
            var transfer = await payroll.ExecuteTransferAsync(caller, new ExecuteBankTransferRequest
            {
                Scope = scope, EmployeeIds = ids, TransferDate = next.AddDays(2), CompanyBankAccountId = "1", TransferMode = "NEFT"
            });

            // Back-date the audit trail to when payroll would really have been run.
            var generatedAt = new DateTimeOffset(next.Year, next.Month, 1, 11, 0, 0, TimeSpan.FromHours(5.5));
            await ExecAsync(@"
                UPDATE payroll.PayrollRuns SET GeneratedUtc = @g, ApprovedUtc = @a, GeneratedBy = 'Simran Kaur', ApprovedBy = 'Simran Kaur' WHERE Id = @r;
                UPDATE payroll.PayrollLines SET UpdatedBy = 'Simran Kaur', UpdatedUtc = @p WHERE RunId = @r;
                UPDATE payroll.BankTransferBatches SET CreatedUtc = @p, CreatedBy = 'Simran Kaur', ReferenceNo = @ref WHERE RunId = @r;",
                ("@g", generatedAt), ("@a", generatedAt.AddDays(1).AddHours(4.5)), ("@p", generatedAt.AddDays(1).AddHours(6.5)),
                ("@r", runId), ("@ref", $"KPS-SAL-{year}{month:00}-01"));

            Console.WriteLine($"   {year}-{month:00}: {generated.Generated} payslips, ₹{transfer.TotalAmount:N0} transferred");
        }

        private async Task SaveBankAccountsAsync(IReadOnlyList<Person> people)
        {
            var caller = Caller(Actor);
            await banks.SaveCompanyAccountAsync(caller, new SaveCompanyBankAccountRequest
            {
                Id = "1", BankName = "HDFC Bank", AccountLabel = "Kapson Corporate Payroll — Chandigarh", AccountNumber = "50200088191920",
                Ifsc = "HDFC0001204", BranchName = "Sector 17, Chandigarh", IsDefault = true, IsActive = true
            });
            await banks.SaveCompanyAccountAsync(caller, new SaveCompanyBankAccountRequest
            {
                Id = "2", BankName = "ICICI Bank", AccountLabel = "Kapson Retail Operations — Mohali", AccountNumber = "008105004412",
                Ifsc = "ICIC0000816", BranchName = "Phase 7, Mohali", IsDefault = false, IsActive = true
            });

            var existing = (await QueryAsync("SELECT EmployeeId, BankName, Ifsc FROM employee.EmployeeBankAccounts",
                r => (Id: r.GetInt64(0), Bank: r.GetString(1), Ifsc: r.GetString(2)))).ToDictionary(x => x.Id);

            foreach (var person in people)
            {
                var (bank, ifsc) = existing.TryGetValue(person.Id, out var row) ? (row.Bank, row.Ifsc) : ("HDFC Bank", "HDFC0001204");
                await banks.SaveEmployeeAccountAsync(caller, new SaveEmployeeBankAccountRequest
                {
                    EmployeeId = person.Id.ToString(), BankName = bank, AccountHolderName = person.Name,
                    AccountNumber = AccountNumberFor(bank, person.Number), Ifsc = ifsc, AccountType = BankAccountTypes.Savings
                });
            }
        }

        // Realistic lengths per bank, stable for a given employee so reruns don't churn.
        private static string AccountNumberFor(string bank, string employeeNumber)
        {
            var digits = string.Concat(SHA256.HashData(Encoding.UTF8.GetBytes(employeeNumber)).Select(b => (b % 10).ToString()));
            return bank switch
            {
                var b when b.StartsWith("HDFC", StringComparison.OrdinalIgnoreCase) => "5010" + digits[..10],
                var b when b.StartsWith("ICICI", StringComparison.OrdinalIgnoreCase) => "0081" + digits[..8],
                var b when b.Contains("State Bank", StringComparison.OrdinalIgnoreCase) || b.StartsWith("SBI") => "3" + digits[..10],
                var b when b.StartsWith("Axis", StringComparison.OrdinalIgnoreCase) => "9120" + digits[..11],
                var b when b.StartsWith("Kotak", StringComparison.OrdinalIgnoreCase) => "7" + digits[..9],
                var b when b.StartsWith("Punjab", StringComparison.OrdinalIgnoreCase) => "0412" + digits[..12],
                _ => "1" + digits[..11]
            };
        }

        private async Task PublishNotificationsAsync(Dictionary<string, string> accountIds, IReadOnlyList<Person> people)
        {
            var t = options.Tenant;
            var hr = new[] { accountIds["EMP-1001"], accountIds["EMP-1002"] };
            var pendingLeave = await QueryAsync(@"SELECT e.LegalFirstName + ' ' + e.LegalLastName FROM workforce.LeaveRequests r
                JOIN employee.Employees e ON e.Id = r.EmployeeId WHERE r.TenantKey = @t AND r.State = 'Pending' ORDER BY r.FromDate", r => r.GetString(0), ("@t", t));

            if (pendingLeave.Count > 0)
            {
                var names = pendingLeave.Count <= 3 ? string.Join(", ", pendingLeave) : $"{string.Join(", ", pendingLeave.Take(3))} and {pendingLeave.Count - 3} more";
                await notifications.PublishAsync(new NotificationRequest(t, "Leave", $"{pendingLeave.Count} leave requests awaiting approval",
                    $"{names} have leave requests waiting for a decision.", "/leave/team/requests", "High", "LeaveQueue", "pending-batch"), hr);
            }

            await notifications.PublishAsync(new NotificationRequest(t, "Onboarding", "Nidhi Chawla submitted the Emergency Contact form",
                "Store Manager joiner (16 Nov) — 3 of 5 checklist items approved; the form is waiting for HR review.", "/onboarding", "Normal", "Onboarding", "4"), hr);
            await notifications.PublishAsync(new NotificationRequest(t, "Interview", "Store Operations Assessment — Amanpreet Singh, 28 Oct 11:00",
                "Round 2 at the Sector 17 flagship store. Panel: Simran Kaur (lead), Rajvir Randhawa.", "/interviews", "Normal", "Interview", "1"),
                [accountIds["EMP-1001"], accountIds["EMP-1023"]]);
            await notifications.PublishAsync(new NotificationRequest(t, "Interview", "Finance Technical Interview — Pranav Kulkarni, 29 Oct 15:00",
                "Online round for the Financial Analyst opening.", "/interviews", "Normal", "Interview", "3"), [accountIds["EMP-1023"]]);
            await notifications.PublishAsync(new NotificationRequest(t, "Payroll", "October payroll is ready to generate",
                "Attendance for October is in. Generate, review and approve October salaries from Payroll → Generate.", "/hr/payroll/generate", "Normal", "Payroll", "2026-10"), hr);

            var employeeRecipients = people.Where(p => !HrTeam.ContainsKey(p.Number) && accountIds.ContainsKey(p.Number) && p.Number != "EMP-1024" && p.Number != "EMP-1025")
                .Select(p => accountIds[p.Number]).ToList();
            await notifications.PublishAsync(new NotificationRequest(t, "Payroll", "Your September payslip is ready",
                "September salary was credited by NEFT on 3 Oct. Tap to view earnings and deductions.", "/payroll/payslips", "Normal", "Payslip", "2026-09"), employeeRecipients);
            await notifications.PublishAsync(new NotificationRequest(t, "System", "Updated company policies available",
                "Please review and acknowledge the published Attendance and Work From Home policies.", "/policies", "Normal", "Policy", "2026-10"), employeeRecipients);
        }

        private static string RoleLabel(string code) => code switch
        {
            "HR_ADMINISTRATOR" => "HR Administrator",
            "HR_EXECUTIVE" => "HR Executive",
            "RECRUITER" => "Recruiter",
            _ => code
        };

        private async Task<List<Person>> LoadPeopleAsync() => await QueryAsync(@"
            SELECT Id, EmployeeNumber, LegalFirstName + ' ' + LegalLastName, Email, ISNULL(JobTitle, ''), DepartmentCode, ManagerEmployeeId
            FROM employee.Employees WHERE TenantKey = @t AND IsActive = 1 ORDER BY EmployeeNumber",
            r => new Person(r.GetInt64(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetString(5), r.IsDBNull(6) ? null : r.GetInt64(6)),
            ("@t", options.Tenant));

        private Task<List<T>> QueryAsync<T>(string sql, Func<SqlDataReader, T> map, params (string, object)[] parameters) =>
            Db.QueryAsync(options.ConnectionString, sql, map, parameters);

        private Task ExecAsync(string sql, params (string, object)[] parameters) =>
            Db.ExecAsync(options.ConnectionString, sql, parameters);
    }

    static class Db
    {
        public static async Task<List<T>> QueryAsync<T>(string connectionString, string sql, Func<SqlDataReader, T> map, params (string Name, object Value)[] parameters)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<T>();
            while (await reader.ReadAsync()) rows.Add(map(reader));
            return rows;
        }

        public static async Task ExecAsync(string connectionString, string sql, params (string Name, object Value)[] parameters)
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new SqlCommand(sql, connection);
            foreach (var (name, value) in parameters) command.Parameters.AddWithValue(name, value);
            await command.ExecuteNonQueryAsync();
        }
    }

    static class PdfPrinter
    {
        public static void Print(string htmlPath, string pdfPath)
        {
            var edge = new[]
            {
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Microsoft\Edge\Application\msedge.exe",
                @"C:\Program Files\Google\Chrome\Application\chrome.exe"
            }.FirstOrDefault(File.Exists) ?? throw new InvalidOperationException("Edge or Chrome is needed to print the PDF.");

            var profile = Path.Combine(Path.GetTempPath(), "demo-data-pdf-" + Guid.NewGuid().ToString("n"));
            var start = new ProcessStartInfo(edge)
            {
                ArgumentList =
                {
                    "--headless=new", "--disable-gpu", "--no-first-run", "--no-pdf-header-footer",
                    $"--user-data-dir={profile}", $"--print-to-pdf={pdfPath}", new Uri(htmlPath).AbsoluteUri
                },
                UseShellExecute = false, RedirectStandardError = true, RedirectStandardOutput = true
            };

            using var process = Process.Start(start)!;
            if (!process.WaitForExit(120_000)) { process.Kill(true); throw new TimeoutException("PDF printing timed out."); }
            try { Directory.Delete(profile, true); } catch { /* best effort */ }
            if (!File.Exists(pdfPath)) throw new InvalidOperationException("PDF was not produced: " + process.StandardError.ReadToEnd());
        }
    }
}
