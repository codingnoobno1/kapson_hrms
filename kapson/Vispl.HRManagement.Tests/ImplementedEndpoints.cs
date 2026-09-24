namespace Vispl.HRManagement.Tests;

/// <summary>
/// The endpoints that return real data today, and the services behind them.
///
/// Most of the mobile API is a complete HTTP contract over modules that are not built yet;
/// those routes answer 501 by design. The entries here are the ones that genuinely work, and
/// the point of this list is that they keep working. A route being re-declared over one of
/// these, or its service being replaced by the not-ready proxy, has already happened once.
///
/// When a module lands, move its endpoints in here.
/// </summary>
public static class ImplementedEndpoints
{
    public static IReadOnlyList<(string Method, string Template)> Routes { get; } =
    [
        // Public career site: apply without an account.
        ("GET",  "api/public/v1/openings/{}"),
        ("POST", "api/public/v1/openings/{}/apply"),
        ("POST", "api/public/v1/candidate/activation-link"),
        ("POST", "api/public/v1/candidate/activate"),

        // Candidate-owned routes, authorised by the candidate_id claim.
        ("GET",  "api/candidate/v1/me"),
        ("POST", "api/candidate/v1/applications/{}/offer"),
        ("GET",  "api/candidate/v1/onboarding/{}"),
        ("POST", "api/candidate/v1/onboarding/{}/tasks/{}/form"),
        ("POST", "api/candidate/v1/onboarding/{}/tasks/{}/document"),

        // Token issuance.
        ("POST", "api/mobile/v1/auth/token"),
        ("POST", "api/mobile/v1/auth/refresh"),
        ("POST", "api/mobile/v1/auth/revoke"),

        // Tenant discovery, before any token exists.
        ("GET",  "api/mobile/v1/discovery/metadata"),
        ("POST", "api/mobile/v1/discovery/resolve-tenant"),

        // Session projection from the caller's own token.
        ("GET",  "api/mobile/v1/session/bootstrap"),
        ("POST", "api/mobile/v1/session/refresh"),

        // Tenant master data.
        ("GET",  "api/mobile/v1/masters"),

        // Recruitment reads that are backed by real persistence.
        ("GET",  "api/mobile/v1/recruitment/jobs"),
        ("GET",  "api/mobile/v1/recruitment/jobs/{}"),
        ("GET",  "api/mobile/v1/recruitment/applications"),
        ("GET",  "api/mobile/v1/recruitment/applications/{}"),
        ("POST", "api/mobile/v1/recruitment/applications/{}/stage"),
        ("GET",  "api/mobile/v1/recruitment/applications/{}/documents/{}"),

        // Home dashboard and notifications.
        ("GET",  "api/mobile/v1/home/hr"),
        ("GET",  "api/mobile/v1/notifications"),
        ("GET",  "api/mobile/v1/notifications/{}"),
        ("POST", "api/mobile/v1/notifications/read"),
        ("DELETE", "api/mobile/v1/notifications/{}"),

        // Interviews.
        ("GET",  "api/mobile/v1/recruitment/interviews"),
        ("POST", "api/mobile/v1/recruitment/interviews"),
        ("GET",  "api/mobile/v1/recruitment/interviews/availability"),
        ("GET",  "api/mobile/v1/recruitment/interviews/{}"),
        ("PUT",  "api/mobile/v1/recruitment/interviews/{}"),
        ("POST", "api/mobile/v1/recruitment/interviews/{}/cancel"),
        ("GET",  "api/mobile/v1/recruitment/interviews/{}/evaluation"),
        ("PUT",  "api/mobile/v1/recruitment/interviews/{}/evaluation"),

        // HR dashboard (web portal, shared with the HR app).
        ("GET",  "api/mobile/v1/dashboard"),
        ("GET",  "api/mobile/v1/dashboard/headline"),
        ("GET",  "api/mobile/v1/dashboard/scopes"),
        ("GET",  "api/mobile/v1/attendance/reports/late-arrivals"),
        ("GET",  "api/mobile/v1/attendance/reports/miss-punch"),
        ("GET",  "api/mobile/v1/leave/analytics/balances/employees"),
        ("GET",  "api/mobile/v1/leave/analytics/balances/departments"),
        ("GET",  "api/mobile/v1/leave/analytics/by-type"),
        ("GET",  "api/mobile/v1/leave/analytics/by-date"),
        ("GET",  "api/mobile/v1/dashboard/moments"),
        ("GET",  "api/mobile/v1/dashboard/leave-analytics"),
        ("GET",  "api/mobile/v1/dashboard/attendance-overview"),
        ("GET",  "api/mobile/v1/dashboard/events"),
        ("GET",  "api/mobile/v1/recruitment/openings/pipeline"),
        ("GET",  "api/mobile/v1/leave/approvals"),
        ("POST", "api/mobile/v1/attendance/close/{}"),

        // Employee KPI tiles (employee app Home and Attendance).
        ("GET",  "api/mobile/v1/me/summary"),
        ("GET",  "api/mobile/v1/me/kpis/attendance"),
        ("GET",  "api/mobile/v1/me/kpis/leave"),
        ("GET",  "api/mobile/v1/me/interviews"),
        ("GET",  "api/mobile/v1/me/app-settings"),
        ("GET",  "api/mobile/v1/me/salary"),
        ("GET",  "api/mobile/v1/me/bank-account"),
        ("GET",  "api/mobile/v1/me/onboarding"),
        ("GET",  "api/mobile/v1/me/attendance/analytics"),
        ("GET",  "api/mobile/v1/me/attendance/report"),
        ("POST", "api/mobile/v1/me/attendance/report/email"),

        // Movement and gate passes.
        ("GET",  "api/mobile/v1/movement/out"),
        ("GET",  "api/mobile/v1/movement/overdue"),
        ("GET",  "api/mobile/v1/movement/me"),
        ("POST", "api/mobile/v1/movement/out"),
        ("POST", "api/mobile/v1/movement/{}/return"),
        ("GET",  "api/mobile/v1/gate-passes"),
        ("GET",  "api/mobile/v1/gate-passes/pending"),
        ("GET",  "api/mobile/v1/gate-passes/me"),
        ("GET",  "api/mobile/v1/gate-passes/{}"),
        ("GET",  "api/mobile/v1/gate-passes/{}/credential"),
        ("POST", "api/mobile/v1/gate-passes"),
        ("POST", "api/mobile/v1/gate-passes/{}/decision"),
        ("POST", "api/mobile/v1/gate-passes/{}/withdraw"),
        ("POST", "api/mobile/v1/gate-passes/scan"),

        // Attendance.
        ("GET",  "api/mobile/v1/attendance/dashboard"),
        ("GET",  "api/mobile/v1/attendance/me"),
        ("GET",  "api/mobile/v1/attendance/team"),
        ("GET",  "api/mobile/v1/attendance/register/{}"),
        ("GET",  "api/mobile/v1/attendance/punch"),
        ("POST", "api/mobile/v1/attendance/punch"),
        ("GET",  "api/mobile/v1/attendance/corrections"),
        ("POST", "api/mobile/v1/attendance/corrections"),
        ("GET",  "api/mobile/v1/attendance/corrections/{}"),
        ("POST", "api/mobile/v1/attendance/corrections/{}/decision"),
        ("POST", "api/mobile/v1/attendance/corrections/{}/withdraw"),
        ("GET",  "api/mobile/v1/attendance/exceptions"),
        ("GET",  "api/mobile/v1/attendance/exceptions/{}"),
        ("POST", "api/mobile/v1/attendance/exceptions/{}/confirm"),
        ("POST", "api/mobile/v1/attendance/exceptions/{}/resolution"),

        // Leave.
        ("GET",  "api/mobile/v1/leave/me/dashboard"),
        ("GET",  "api/mobile/v1/leave/balances"),
        ("GET",  "api/mobile/v1/leave/requests"),
        ("POST", "api/mobile/v1/leave/requests"),
        ("GET",  "api/mobile/v1/leave/requests/{}"),
        ("POST", "api/mobile/v1/leave/requests/calculate"),
        ("POST", "api/mobile/v1/leave/requests/{}/withdraw"),
        ("POST", "api/mobile/v1/leave/requests/{}/comments"),
        ("GET",  "api/mobile/v1/leave/team/requests"),
        ("POST", "api/mobile/v1/leave/requests/{}/decision"),
        ("GET",  "api/mobile/v1/leave/team/calendar"),
        ("GET",  "api/mobile/v1/leave/adjustments"),
        ("POST", "api/mobile/v1/leave/adjustments"),
        ("GET",  "api/mobile/v1/leave/policies"),
        ("GET",  "api/mobile/v1/leave/policies/{}"),

        // Employee records.
        ("GET",  "api/mobile/v1/employees"),
        ("POST", "api/mobile/v1/employees"),
        ("POST", "api/mobile/v1/employees/duplicate-check"),
        ("GET",  "api/mobile/v1/employees/{}"),
        ("GET",  "api/mobile/v1/employees/{}/personal"),
        ("PUT",  "api/mobile/v1/employees/{}/personal"),
        ("GET",  "api/mobile/v1/employees/{}/employment"),
        ("PUT",  "api/mobile/v1/employees/{}/employment"),
        ("GET",  "api/mobile/v1/employees/{}/organisation"),
        ("PUT",  "api/mobile/v1/employees/{}/organisation"),
        ("GET",  "api/mobile/v1/employees/{}/contacts"),
        ("PUT",  "api/mobile/v1/employees/{}/contacts"),
        ("DELETE", "api/mobile/v1/employees/{}/contacts/{}"),
        ("GET",  "api/mobile/v1/employees/{}/dependants"),
        ("GET",  "api/mobile/v1/employees/{}/timeline"),
        ("POST", "api/mobile/v1/employees/{}/change-requests"),
        ("GET",  "api/mobile/v1/employees/change-requests/{}"),

        // Who may carry company data on a phone.
        ("GET",  "api/mobile/v1/employees/mobile-access"),
        ("POST", "api/mobile/v1/employees/mobile-access/{}/grant"),
        ("POST", "api/mobile/v1/employees/mobile-access/{}/suspend"),
        ("POST", "api/mobile/v1/employees/mobile-access/{}/restore"),

        // Onboarding.
        ("GET",  "api/mobile/v1/onboarding/{}"),
        ("GET",  "api/mobile/v1/onboarding/by-application/{}"),
        ("POST", "api/mobile/v1/onboarding/{}/tasks/{}/complete"),
        ("POST", "api/mobile/v1/onboarding/{}/tasks/{}/decision")
    ];

    /// <summary>
    /// Business services that must have a real implementation. A not-ready registration for
    /// any of these turns a working endpoint into a 501 without breaking the build.
    /// </summary>
    public static IReadOnlyList<Type> RealServices { get; } =
    [
        typeof(BM.ITF.Recruitment.IApplicationService),
        typeof(BM.ITF.Recruitment.IOnboardingService),
        typeof(BM.ITF.Recruitment.ICandidateDirectory),
        typeof(BM.ITF.Recruitment.IJobOpeningService),
        typeof(BM.ITF.Admin.IMasterDataService),
        typeof(BM.ITF.Admin.ITenantService),
        typeof(BM.ITF.Admin.ICareerSiteService),
        typeof(BM.ITF.Admin.IThemeTemplateService),
        typeof(BM.ITF.Admin.IMediaLibraryService),
        typeof(BM.ITF.Mobile.IMobileSessionService),
        typeof(BM.ITF.Mobile.IMobileEmployeeService),
        typeof(BM.ITF.Employee.IEmployeeProvisioning),
        typeof(BM.ITF.Mobile.IInterviewService),
        typeof(BM.ITF.Mobile.IMobileNotificationService),
        typeof(BM.ITF.Mobile.IMobileHomeService),
        typeof(BM.ITF.Mobile.IAttendanceService),
        typeof(BM.ITF.Mobile.IEmployeeKpiService),
        typeof(BM.ITF.Mobile.IHrDashboardService),
        typeof(BM.ITF.Mobile.IWorkforceReportService),
        typeof(BM.ITF.Mobile.IShiftScheduleService),
        typeof(BM.ITF.Mobile.IMovementService),
        typeof(BM.ITF.Mobile.IGatePassService),
        typeof(BM.ITF.Mobile.IMobileLeaveService),
        typeof(BM.ITF.Mobile.ILeavePolicyService),
        typeof(BM.ITF.Mobile.IEmployeeMobileAccessService),
        typeof(BM.ITF.Workforce.IAttendanceDayCloser),
        typeof(BM.ITF.Shared.INotificationPublisher)
        // IPermissionAudience is deliberately absent: it is registered by the identity
        // module, not the business one, and this list only inspects business registrations.
    ];
}
