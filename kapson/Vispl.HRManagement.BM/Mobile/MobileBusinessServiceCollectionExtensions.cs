using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.BM.ITF.Mobile;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Registers the business services behind the mobile API surface.
///
/// Modules that are implemented register their real service here. Modules that are not
/// yet implemented register through <c>AddNotReady</c>, which keeps the route, the DTO
/// contract and the generated client complete while failing loudly with 501 at runtime.
/// Moving a module from planned to built is a one-line change in this file.
/// </summary>
public static class MobileBusinessServiceCollectionExtensions
{
    public static IServiceCollection AddMobileBusiness(this IServiceCollection services)
    {
        AddSharedExperience(services);
        AddRecruitment(services);
        AddEmployeeLifecycle(services);
        AddWorkforce(services);
        AddPayroll(services);
        AddOperations(services);
        return services;
    }

    private static void AddSharedExperience(IServiceCollection services)
    {
        services.AddNotReady<IMobileSessionService>(
            "Session", "session projection", "offline grant policy");
        services.AddNotReady<IMobileDeviceService>(
            "Devices", "device registry entity", "push registration");
        services.AddNotReady<IMobileConfigurationService>(
            "Configuration", "published tenant configuration aggregate");
        services.AddNotReady<IMobileHomeService>(
            "Home", "home projections", "permission-shaped KPI service");
        services.AddNotReady<IMobileSearchService>(
            "Search", "federated search index", "permission filtering");
        services.AddNotReady<IMobileNotificationService>(
            "Notifications", "notification store", "delivery preferences");
        services.AddNotReady<IMobileCalendarService>(
            "Calendar", "unified event projection");
        services.AddNotReady<IMobileTaskService>(
            "Tasks", "task store", "workflow task projection");
    }

    private static void AddRecruitment(IServiceCollection services)
    {
        services.AddNotReady<IRecruitmentDashboardService>(
            "Recruitment Dashboard", "recruitment projections", "pipeline funnel");
        services.AddNotReady<IRequisitionService>(
            "Requisitions", "requisition entity", "requisition repository", "approval workflow");
        services.AddNotReady<IMobileJobService>(
            "Jobs (mobile)", "job draft workflow", "publication channels", "job analytics");
        services.AddNotReady<IMobileApplicationService>(
            "Applications (mobile)", "pipeline stage machine", "screening scorecard");
        services.AddNotReady<ICandidateService>(
            "Candidates", "candidate PII field policy", "protected résumé streaming");
        services.AddNotReady<ICandidateDocumentService>(
            "Candidate Documents", "private document storage", "verification service", "malware scanning");
        services.AddNotReady<IInterviewService>(
            "Interviews", "interview entity", "panel availability service", "evaluation workflow");
        services.AddNotReady<IOfferService>(
            "Offers", "offer entity", "offer approval chain", "protected document generation");
    }

    private static void AddEmployeeLifecycle(IServiceCollection services)
    {
        services.AddNotReady<IMobileEmployeeService>(
            "Employees", "employee entity", "employee repository", "field permission policy");
        services.AddNotReady<IEmployeeDocumentService>(
            "Employee Documents", "document storage", "verification workflow");
        services.AddNotReady<IMobileOnboardingService>(
            "Onboarding (mobile)", "onboarding cohort projection", "task assignment");
        services.AddNotReady<IProbationService>(
            "Probation", "probation entity", "confirmation workflow");
        services.AddNotReady<IOffboardingService>(
            "Offboarding", "separation entity", "clearance checklist", "final settlement hand-off");
    }

    private static void AddWorkforce(IServiceCollection services)
    {
        services.AddNotReady<IAttendanceService>(
            "Attendance", "attendance entity", "signed punch validation", "attendance repository");
        services.AddNotReady<IShiftService>(
            "Shifts", "shift entity", "roster service", "swap eligibility rules");
        services.AddNotReady<IMobileLeaveService>(
            "Leave", "leave policy and request entities", "balance service", "approval workflow");
        services.AddNotReady<ILeavePolicyService>(
            "Leave Policies", "leave policy entity", "eligibility service");
    }

    private static void AddPayroll(IServiceCollection services)
    {
        services.AddNotReady<IMobilePayrollService>(
            "Payroll", "payroll period entity", "payslip generation", "tax computation");
        services.AddNotReady<IReimbursementService>(
            "Reimbursements", "claim entity", "receipt storage", "duplicate detection");
        services.AddNotReady<IBenefitService>(
            "Benefits", "benefit plan entity", "enrolment window policy");
    }

    private static void AddOperations(IServiceCollection services)
    {
        services.AddNotReady<IApprovalService>(
            "Approvals", "workflow engine", "approver resolution", "delegation and escalation");
        services.AddNotReady<IHrRequestService>(
            "HR Requests", "service catalog", "dynamic form schema store", "SLA engine");
        services.AddNotReady<IReportService>(
            "Reports", "report definitions", "permission-scoped execution");
        services.AddNotReady<IExportService>(
            "Exports", "export job queue", "protected artefact storage");
        services.AddNotReady<IMobileProfileService>(
            "Profile", "own-profile projection", "preference store");
        services.AddNotReady<ISupportService>(
            "Support", "help article store", "ticket integration");
        services.AddNotReady<ISyncService>(
            "Sync", "delta token store", "change feed", "idempotency ledger");
        services.AddNotReady<IProtectedFileService>(
            "Protected Files", "one-time ticket store", "ranged secure streaming");
    }
}
