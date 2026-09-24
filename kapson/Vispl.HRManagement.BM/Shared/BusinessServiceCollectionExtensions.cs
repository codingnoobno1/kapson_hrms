using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.BM.Admin;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.BM.Mobile;
using Vispl.HRManagement.BM.Recruitment;

namespace Vispl.HRManagement.BM.Shared;

public static class BusinessServiceCollectionExtensions
{
    public static IServiceCollection AddHrManagementBusiness(this IServiceCollection services)
    {
        services.AddScoped<ICareerSiteService, CareerSiteService>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Shared.ITenantClock, Shared.TenantClock>();
        services.AddScoped<IMasterDataService, MasterDataService>();
        services.AddScoped<IPolicyService, PolicyService>();
        services.AddScoped<IDesignationService, DesignationService>();
        services.AddScoped<IGradeSalaryService, GradeSalaryService>();
        services.AddScoped<IBankAccountService, BankAccountService>();
        services.AddScoped<IEmployeeAppSettingsService, EmployeeAppSettingsService>();
        services.AddScoped<IPayrollService, PayrollService>();
        services.AddScoped<IEmployeeReportService, EmployeeReportService>();
        services.AddScoped<ILoanService, LoanService>();

        // Stateless workbook builder for every Excel download (Syncfusion XlsIO).
        services.AddSingleton<Vispl.HRManagement.BM.ITF.Shared.IExcelExporter, Shared.SyncfusionExcelExporter>();
        services.AddScoped<IThemeTemplateService, ThemeTemplateService>();
        services.AddScoped<IMediaLibraryService, MediaLibraryService>();
        services.AddScoped<IJobOpeningService, JobOpeningService>();
        services.AddScoped<ILeaveConfigurationService, LeaveConfigurationService>();
        services.AddScoped<IShiftConfigurationService, ShiftConfigurationService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<IOnboardingService, OnboardingService>();
        services.AddScoped<ICandidateDirectory, CandidateDirectory>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Employee.IEmployeeProvisioning, Employee.EmployeeProvisioning>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Workforce.IAttendanceDayCloser, Workforce.AttendanceDayCloser>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Workforce.IOverdueMovementNotifier, Workforce.OverdueMovementNotifier>();

        // Registered before AddNotReady runs, which skips any interface that already has an
        // implementation. Session bootstrap projects the caller's token, so it needs no module.
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMobileSessionService, Mobile.MobileSessionService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMobileEmployeeService, Mobile.MobileEmployeeService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IInterviewService, Mobile.InterviewService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMobileNotificationService, Mobile.MobileNotificationService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Shared.INotificationPublisher, NotificationPublisher>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMobileHomeService, Mobile.MobileHomeService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IShiftScheduleService, Mobile.ShiftScheduleService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IAttendanceService, Mobile.AttendanceService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IEmployeeKpiService, Mobile.EmployeeKpiService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IEmployeeSelfService, Mobile.EmployeeSelfService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IAttendanceAnalyticsService, Mobile.AttendanceAnalyticsService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IHrDashboardService, Mobile.HrDashboardService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IWorkforceReportService, Mobile.WorkforceReportService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMovementService, Mobile.MovementService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IGatePassService, Mobile.GatePassService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IOnDutyService, Mobile.OnDutyService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IEmployeeMobileAccessService, Employee.EmployeeMobileAccessService>();
        // One class serves both leave interfaces: policy reads and the request lifecycle share
        // the same rules, and splitting them would duplicate the calculation. Registered twice
        // by type rather than shared through a factory, because the service is stateless and a
        // named implementation type is what distinguishes a real service from the not-ready proxy.
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMobileLeaveService, Mobile.LeaveService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.ILeavePolicyService, Mobile.LeaveService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMobileConfigurationService, Mobile.MobileConfigurationService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IRecruitmentDashboardService, Mobile.RecruitmentDashboardService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMobileSearchService, Mobile.MobileSearchService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMobileCalendarService, Mobile.MobileCalendarService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMobileProfileService, Mobile.MobileProfileService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IProbationService, Mobile.ProbationService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IMobilePayrollService, Mobile.MobilePayrollService>();
        services.AddScoped<Vispl.HRManagement.BM.ITF.Mobile.IApprovalService, Mobile.ApprovalService>();
        services.AddMobileBusiness();
        return services;
    }
}
