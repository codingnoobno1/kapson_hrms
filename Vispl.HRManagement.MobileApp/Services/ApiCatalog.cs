namespace Vispl.HRManagement.MobileApp.Services;

public sealed record ApiModule(
    string Key,
    string Name,
    string Icon,
    string Path,
    string? Permission,
    bool IsImplemented);

public sealed record ApiModuleGroup(string Name, IReadOnlyList<ApiModule> Modules);

/// <summary>
/// Mirrors the routes registered by Vispl.HRManagement.Api, so the shell can report what the
/// server actually offers instead of guessing.
///
/// Two rules keep this honest, and both were broken before:
///
/// <list type="bullet">
/// <item><c>Path</c> must be a route that exists. A module prefix such as
/// <c>api/mobile/v1/leave</c> has no endpoint of its own, so probing it returned 404 and the
/// readiness screen reported "missing" for one of the most complete modules in the product.
/// Every entry now names a real, reachable endpoint.</item>
/// <item><c>IsImplemented</c> must match what that endpoint returns. It said two of thirty-three
/// were built while nine were already serving real data.</item>
/// </list>
///
/// Verified against a running host on 2026-09-05.
/// </summary>
public static class ApiCatalog
{
    public const string MetadataPath = "api/mobile/v1/discovery/metadata";
    public const string ResolveTenantPath = "api/mobile/v1/discovery/resolve-tenant";
    public const string MastersPath = "api/mobile/v1/masters";
    public const string JobsPath = "api/mobile/v1/recruitment/jobs";
    public const string SessionPath = "api/mobile/v1/session/bootstrap";

    public static IReadOnlyList<ApiModuleGroup> Groups { get; } =
    [
        new("Workspace",
        [
            new("session", "Session", "lock", SessionPath, null, true),
            new("home-hr", "HR Home", "home", "api/mobile/v1/home/hr", null, true),
            new("notifications", "Notifications", "bell", "api/mobile/v1/notifications", null, true),
            new("dashboard", "HR Dashboard", "bar-chart", "api/mobile/v1/dashboard", "DASHBOARD.VIEW", true),
            new("me", "My Summary", "user", "api/mobile/v1/me/summary", null, true),
            new("configuration", "App Configuration", "settings", "api/mobile/v1/configuration", null, true),
            new("home-employee", "Employee Home", "user", "api/mobile/v1/home/employee", null, false),
            new("home-manager", "Manager Home", "user", "api/mobile/v1/home/manager", null, false),
            new("search", "Global Search", "search", "api/mobile/v1/search", null, true),
            new("calendar", "Calendar", "calendar", "api/mobile/v1/calendar", null, true),
            new("tasks", "Tasks", "check", "api/mobile/v1/tasks", null, false),
            new("devices", "Devices", "smartphone", "api/mobile/v1/devices", null, false)
        ]),
        new("Recruitment",
        [
            new("masters", "Master Data", "folder", MastersPath, null, true),
            new("jobs", "Job Openings", "clipboard", JobsPath, "RECRUITMENT.VIEW", true),
            new("applications", "Applications", "inbox", "api/mobile/v1/recruitment/applications", "RECRUITMENT.APPLICATIONS", true),
            new("interviews", "Interviews", "message-circle", "api/mobile/v1/recruitment/interviews", "RECRUITMENT.VIEW", true),
            new("openings-pipeline", "Openings Board", "user-circle", "api/mobile/v1/recruitment/openings/pipeline", "RECRUITMENT.VIEW", true),
            new("recruitment-dashboard", "Recruitment Dashboard", "bar-chart", "api/mobile/v1/recruitment/dashboard", "RECRUITMENT.VIEW", true),
            new("requisitions", "Requisitions", "file-text", "api/mobile/v1/recruitment/requisitions", "RECRUITMENT.VIEW", false),
            new("candidates", "Candidates", "user", "api/mobile/v1/recruitment/candidates/1", "RECRUITMENT.APPLICATIONS", false),
            new("offers", "Offers", "award", "api/mobile/v1/recruitment/offers", "RECRUITMENT.MANAGE", false)
        ]),
        new("People",
        [
            new("employees", "Employees", "users", "api/mobile/v1/employees", "EMPLOYEES.VIEW", true),
            new("mobile-access", "Employee App Access", "user-plus", "api/mobile/v1/employees/mobile-access", "EMPLOYEES.VIEW", true),
            new("onboarding", "Onboarding", "trending-up", "api/mobile/v1/onboarding/1", "EMPLOYEES.VIEW", true),
            new("probation", "Probation", "clock", "api/mobile/v1/probation/1", "PROBATION.VIEW", true),
            new("offboarding", "Offboarding", "log-out", "api/mobile/v1/offboarding/cases", "EMPLOYEES.DEACTIVATE", false)
        ]),
        new("Time and Leave",
        [
            new("attendance", "Attendance", "clock", "api/mobile/v1/attendance/dashboard", "ATTENDANCE.VIEW", true),
            new("leave", "Leave Requests", "sun", "api/mobile/v1/leave/requests", null, true),
            new("leave-approvals", "Leave Approvals", "check-square", "api/mobile/v1/leave/approvals", "LEAVE.APPROVE", true),
            new("leave-policies", "Leave Policies", "file-text", "api/mobile/v1/leave/policies", null, true),
            new("movement", "Movement", "activity", "api/mobile/v1/movement/out", "MOVEMENT.TEAM", true),
            new("gate-passes", "Gate Passes", "ticket", "api/mobile/v1/gate-passes", "GATEPASS.TEAM", true),
            new("shifts", "Shifts", "refresh", "api/mobile/v1/shifts/roster", null, false)
        ]),
        new("Pay and Benefits",
        [
            new("payroll", "Payroll", "dollar-sign", "api/mobile/v1/payroll/payslips", null, true),
            new("reimbursements", "Reimbursements", "calculator", "api/mobile/v1/reimbursements", null, false),
            new("benefits", "Benefits", "gift", "api/mobile/v1/benefits/coverage", null, false)
        ]),
        new("Operations",
        [
            new("approvals", "Approvals", "check-square", "api/mobile/v1/approvals", "LEAVE.APPROVE", true),
            new("requests", "HR Requests", "send", "api/mobile/v1/requests", null, false),
            new("reports", "Reports", "trending-up", "api/mobile/v1/reports/catalog", "REPORTS.VIEW", false),
            new("exports", "Exports", "upload", "api/mobile/v1/exports", null, false),
            new("profile", "My Profile", "user", "api/mobile/v1/profile", null, true),
            new("support", "Support", "help", "api/mobile/v1/support/articles", null, false),
            new("sync", "Offline Sync", "refresh", "api/mobile/v1/sync/manifest", null, false),
            new("files", "Protected Files", "lock", "api/mobile/v1/files/tickets", null, false)
        ])
    ];

    public static IEnumerable<ApiModule> All => Groups.SelectMany(group => group.Modules);

    public static ApiModule? Find(string key) =>
        All.FirstOrDefault(module => string.Equals(module.Key, key, StringComparison.OrdinalIgnoreCase));

    public static int ImplementedCount => All.Count(module => module.IsImplemented);
    public static int TotalCount => All.Count();
}
