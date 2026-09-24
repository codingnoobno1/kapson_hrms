namespace Vispl.HRManagement.UI.Components.Hr.Shared;

/// <summary>
/// One card in the Employee / Salary Management / Employee Report tabs of the mega-menu — the
/// three tabs that are a flat list of destinations rather than a 26-item master catalogue (that
/// one is <see cref="HrMasterRegistry"/>). <see cref="Href"/> always resolves to something real:
/// either an existing page, or the shared "not built yet" placeholder — never a dead link.
/// </summary>
public sealed record HrMenuCard(string Title, string Subtitle, string Icon, string Href, bool IsReady = true);

/// <summary>
/// scopedocs/web-portal/08-app-menu-design-layout.md §3.4 — the Employee, Salary Management and
/// Employee Report tabs, transcribed card-for-card. None of these destinations exist yet except
/// "Employee Explorer", which is the existing People directory under a different name.
/// </summary>
public static class HrMegaMenuRegistry
{
    public static IReadOnlyList<HrMenuCard> EmployeeCards(string? tenantKey) =>
    [
        new("Employee Explorer", "Employee Explorer", "🗂️", Path(tenantKey, "people")),
        new("Organisation Hierarchy", "Organisation Hierarchy", "🏛️", Path(tenantKey, "employees/hierarchy")),
        new("Add New Employee", "Add New Employee", "➕", Path(tenantKey, "employees/new"))
    ];

    public static IReadOnlyList<HrMenuCard> SalaryManagementCards(string? tenantKey) =>
    [
        new("Generate Employee Salary", "Individual Employee-wise payroll generation", "💰", Path(tenantKey, "payroll/generate")),
        new("Loan and Advance", "Loan and advanced voucher", "🏦", Path(tenantKey, "payroll/loans"))
    ];

    public static IReadOnlyList<HrMenuCard> EmployeeReportCards(string? tenantKey) =>
    [
        new("Employee Detail Report", "Employee Detail report", "👤", Path(tenantKey, "reports/employee-detail")),
        new("Employee Monthly Attendance Report", "Employee Monthly Attendance Report", "📅", Path(tenantKey, "reports/attendance-monthly")),
        new("Employees Salary Register", "Employee Salary report", "📊", Path(tenantKey, "reports/salary-register")),
        new("Loan and Advance Report", "Loan and Advance Report", "🏦", Path(tenantKey, "reports/loans"))
    ];

    private static string Path(string? tenantKey, string suffix) =>
        string.IsNullOrWhiteSpace(tenantKey) ? $"/hr/{suffix}" : $"/hr/{tenantKey}/{suffix}";

    private static string NotReady(string? tenantKey, string title) =>
        $"{Path(tenantKey, "coming-soon")}?title={Uri.EscapeDataString(title)}";
}
