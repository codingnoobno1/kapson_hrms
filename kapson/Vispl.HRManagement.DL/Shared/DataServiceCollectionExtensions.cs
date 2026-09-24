using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.DL.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Recruitment;
using Vispl.HRManagement.DL.Recruitment;

namespace Vispl.HRManagement.DL.Shared;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddHrManagementData(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("HrManagement");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:HrManagement is required. " +
                "For production, set ConnectionStrings__HrManagement in the host environment.");
        }

        services.AddDbContext<HrManagementDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                sql.EnableRetryOnFailure(5, TimeSpan.FromSeconds(10), null)));

        services.AddScoped<ICareerSiteRepository, CareerSiteRepository>();
        services.AddScoped<IMailSettingsRepository, MailSettingsRepository>();
        services.AddScoped<IEmployeeAppSettingsRepository, EmployeeAppSettingsRepository>();
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<IMasterOptionRepository, MasterOptionRepository>();
        services.AddScoped<IPolicyRepository, PolicyRepository>();
        services.AddScoped<IDesignationRepository, DesignationRepository>();
        services.AddScoped<IGradeSalaryRepository, GradeSalaryRepository>();
        services.AddScoped<IBankAccountRepository, BankAccountRepository>();
        services.AddScoped<IPayrollRepository, PayrollRepository>();
        services.AddScoped<ILoanRepository, LoanRepository>();
        services.AddScoped<IThemeTemplateRepository, ThemeTemplateRepository>();
        services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
        services.AddSingleton<IMediaStorage, LocalMediaStorage>();
        services.AddScoped<IJobOpeningRepository, JobOpeningRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IInterviewRepository, InterviewRepository>();
        services.AddScoped<Vispl.HRManagement.DL.ITF.Shared.INotificationRepository, NotificationRepository>();
        services.AddScoped<Vispl.HRManagement.DL.ITF.Workforce.IAttendanceRepository, Workforce.AttendanceRepository>();
        services.AddScoped<Vispl.HRManagement.DL.ITF.Workforce.ILeaveRepository, Workforce.LeaveRepository>();
        services.AddScoped<Vispl.HRManagement.DL.ITF.Workforce.IShiftRepository, Workforce.ShiftRepository>();
        services.AddScoped<Vispl.HRManagement.DL.ITF.Workforce.IMovementRepository, Workforce.MovementRepository>();
        services.AddScoped<Vispl.HRManagement.DL.ITF.Workforce.IOnDutyRepository, Workforce.OnDutyRepository>();
        services.AddScoped<Vispl.HRManagement.DL.ITF.Employee.IEmployeeRepository, Employee.EmployeeRepository>();
        services.AddScoped<HrManagementDatabaseInitializer>();
        return services;
    }
}
