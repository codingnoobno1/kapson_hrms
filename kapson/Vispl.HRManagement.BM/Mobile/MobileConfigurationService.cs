using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.Contracts.Mobile.Configuration;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// The published shape of the app for one client: which modules are on, what the shell looks
/// like, and which policies the device must honour.
///
/// It is a projection over the switches HR already maintains rather than a second copy of
/// them, so turning something off in Employee App Controls is the only place that decision is
/// recorded. Navigation and dashboard entries are filtered by the caller's permissions, so the
/// shell never offers a destination that would answer 403 when opened.
/// </summary>
public sealed class MobileConfigurationService(
    ITenantService tenants,
    IEmployeeAppSettingsService appSettings) : IMobileConfigurationService
{
    public async Task<MobileConfigurationDto> GetPublishedAsync(
        MobileCallerContext caller,
        long? knownVersion,
        CancellationToken ct)
    {
        var tenant = await tenants.GetByKeyAsync(caller.TenantKey, ct)
            ?? throw new MobileNotFoundException("This client no longer exists.");

        var settings = await appSettings.GetAsync(caller.TenantKey, ct);

        // knownVersion is what the caller already holds. The version travels back on every
        // response, so the app can compare and skip re-rendering; the payload is small enough
        // that withholding it would cost a second round trip more often than it saved one.
        _ = knownVersion;

        return new MobileConfigurationDto(
            VersionOf(settings),
            tenant.TenantKey,
            tenant.DisplayName,
            Theme(),
            Modules(settings),
            Navigation(caller),
            Dashboard(caller),
            Security(),
            Offline(),
            Release(),
            Support());
    }

    /// <summary>
    /// Moves whenever HR saves, which is exactly when a device's copy goes stale. Defaults that
    /// were never saved have no timestamp, so they publish as version 1.
    /// </summary>
    private static long VersionOf(EmployeeAppSettingsDto settings) =>
        settings.UpdatedUtc is { } saved ? saved.UtcTicks : 1L;

    private static MobileThemeDto Theme() => new(
        LogoUrl: null,
        PrimaryColor: "#3478ED",
        AccentColor: "#26D9EA",
        SurfaceColor: "#FFFFFF",
        BackgroundColor: "#FCFCFC",
        TextColor: "#3F3F3F",
        ThemeMode: "light");

    private static IReadOnlyList<MobileModuleDto> Modules(EmployeeAppSettingsDto settings)
    {
        const string OffByHr = "Your HR team has turned this off.";

        return
        [
            Module("attendance", "Attendance", settings.AllowAppPunch, "/attendance", "ATTENDANCE.VIEW"),
            Module("attendance-corrections", "Attendance Corrections", settings.AllowAttendanceCorrections, null, null),
            Module("leave", "Leave", true, "/leave", null),
            Module("gate-pass", "Gate Passes", settings.AllowGatePassRequests, "/gate-passes", "GATEPASS.TEAM"),
            Module("payroll", "Salary", settings.ShowSalaryDetails, null, null),
            Module("bank", "Bank Details", settings.ShowBankDetails, null, null),
            Module("onboarding", "Onboarding", settings.ShowOnboarding, "/onboarding", "EMPLOYEES.VIEW"),
            Module("profile-requests", "Profile Change Requests", settings.AllowProfileChangeRequests, null, null)
        ];

        static MobileModuleDto Module(
            string code, string name, bool enabled, string? route, string? permission) =>
            new(code, name, enabled, enabled ? route : null, permission, enabled ? null : OffByHr);
    }

    private static MobileNavigationDto Navigation(MobileCallerContext caller)
    {
        var tabs = new List<MobileNavigationItemDto>
        {
            new("home", "Home", "home", "/", 1, null, null, null),
            new("jobs", "Manage", "file-text", "/jobs", 2, "RECRUITMENT.VIEW", "recruitment", null),
            new("interviews", "Interview Status", "refresh", "/interviews", 3, "RECRUITMENT.VIEW", "recruitment", null),
            new("menu", "Menu", "menu", "/menu", 4, null, null, null)
        };

        var flyout = new List<MobileNavigationItemDto>
        {
            new("profile", "Profile", "user", "/modules/profile", 1, null, null, null),
            new("applicants", "Stall Applicants", "users", "/applicants", 2, "RECRUITMENT.APPLICATIONS", "recruitment", null),
            new("people", "People", "users", "/people", 3, "EMPLOYEES.VIEW", null, null),
            new("leave-approvals", "Leave Approvals", "sun", "/leave", 4, "LEAVE.APPROVE", "leave", "leave.pending"),
            new("attendance", "Attendance", "clock", "/attendance", 5, "ATTENDANCE.VIEW", "attendance", null),
            new("masters", "Master Data", "folder", "/masters", 6, null, null, null),
            new("settings", "Setting", "settings", "/settings", 7, null, null, null)
        };

        return new MobileNavigationDto(
            PersonaOf(caller),
            "/",
            Permitted(tabs, caller),
            Permitted(flyout, caller));
    }

    private static MobileDashboardLayoutDto Dashboard(MobileCallerContext caller)
    {
        var widgets = new List<MobileDashboardWidgetDto>
        {
            new("kpi-jobs", "kpi.total-jobs", "Total Jobs", "small", 1, "/jobs", "RECRUITMENT.VIEW"),
            new("kpi-active", "kpi.active-jobs", "Active Jobs", "small", 2, "/jobs", "RECRUITMENT.VIEW"),
            new("kpi-applicants", "kpi.total-applicants", "Total Applicants", "small", 3, "/applicants", "RECRUITMENT.APPLICATIONS"),
            new("interviews", "list.interview-schedule", "Interview Schedule", "wide", 4, "/interviews", "RECRUITMENT.VIEW"),
            new("analytics", "chart.applicants-by-title", "Applicants Analytics", "wide", 5, null, "RECRUITMENT.VIEW")
        };

        return new MobileDashboardLayoutDto(
            PersonaOf(caller),
            widgets.Where(widget => Allowed(widget.RequiredPermission, caller)).ToList());
    }

    /// <summary>
    /// The shell the caller gets. HR sees the recruitment-led layout; a manager and a plain
    /// employee both land on their own record, and the permission filter does the rest.
    /// </summary>
    private static string PersonaOf(MobileCallerContext caller) =>
        caller.Has("HOME.HR") ? "hr"
        : caller.Has("HOME.MANAGER") ? "manager"
        : "employee";

    private static IReadOnlyList<MobileNavigationItemDto> Permitted(
        IEnumerable<MobileNavigationItemDto> items, MobileCallerContext caller) =>
        items.Where(item => Allowed(item.RequiredPermission, caller)).ToList();

    private static bool Allowed(string? permission, MobileCallerContext caller) =>
        permission is null || caller.Has(permission);

    private static MobileSecurityPolicyDto Security() => new(
        DeviceRegistrationRequired: false,
        PinRequired: false,
        BiometricAllowed: true,
        IdleTimeoutMinutes: 30,
        MaximumRegisteredDevices: 5,
        ScreenshotProtectionRequired: false);

    /// <summary>
    /// Offline reads stay switched off while the sync module is unbuilt. Publishing approved
    /// areas the device cannot actually populate would have it cache nothing and report success.
    /// </summary>
    private static MobileOfflinePolicyDto Offline() => new(
        IsEnabled: false,
        MaximumCacheBytes: 0,
        DefaultReadTtlMinutes: 0,
        ApprovedReadAreas: [],
        ApprovedOutboxActions: []);

    private static MobileReleasePolicyDto Release() => new(
        MinimumVersion: "1.0.0",
        RecommendedVersion: "1.0.0",
        ForceUpdate: false,
        AndroidStoreUrl: null,
        IosStoreUrl: null,
        MaintenanceMessage: null);

    private static MobileSupportConfigurationDto Support() => new(
        SupportEmail: null,
        SupportUrl: null,
        PrivacyUrl: null,
        TermsUrl: null);
}
