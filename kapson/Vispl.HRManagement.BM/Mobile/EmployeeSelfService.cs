using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.BM.ITF.Recruitment;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Employee;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Salary, bank and onboarding for the signed-in employee.
///
/// Salary is calculated by the same <c>SalaryCalculator</c> payroll uses, from the grade's
/// structure, so the sheet cannot promise a figure payroll would not produce. Bank details are
/// masked here, on the server; the full number never travels to a phone.
/// </summary>
public sealed class EmployeeSelfService(
    IEmployeeRepository employees,
    IEmployeeAppSettingsService appSettings,
    IGradeSalaryService grades,
    IBankAccountRepository bankAccounts,
    IOnboardingService onboarding) : IEmployeeSelfService
{
    public async Task<EmployeeAppFeaturesDto> GetFeaturesAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var settings = await appSettings.GetAsync(caller.TenantKey, ct);

        return new EmployeeAppFeaturesDto(
            settings.AllowAppPunch,
            settings.RequireLocationForPunch,
            settings.AllowGatePassRequests,
            settings.AllowAttendanceCorrections,
            settings.ShowSalaryDetails,
            settings.ShowBankDetails,
            settings.ShowOnboarding,
            settings.AllowProfileChangeRequests,
            settings.AttendanceTargetPercent);
    }

    public async Task<MySalaryDto> GetMySalaryAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var settings = await appSettings.GetAsync(caller.TenantKey, ct);
        RequireEnabled(settings.ShowSalaryDetails, "Salary Details");

        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var account = await bankAccounts.GetEmployeeAccountAsync(caller.TenantKey, employee.Id, ct);

        var paymentMode = account is null ? "Not set up — contact HR" : "Bank transfer";
        const string frequency = "Monthly";
        const string schedule = "Paid after HR approves the month's payroll, prorated for attendance";

        var structure = string.IsNullOrWhiteSpace(employee.GradeCode)
            ? null
            : (await grades.GetAllAsync(caller.TenantKey, ct))
                .FirstOrDefault(item => string.Equals(item.GradeCode, employee.GradeCode, StringComparison.OrdinalIgnoreCase));

        if (structure is not { IsConfigured: true })
        {
            return new MySalaryDto(false, employee.GradeCode, structure?.GradeName, 0, [], 0, [], 0, 0, 0,
                frequency, paymentMode, schedule);
        }

        var breakdown = await grades.CalculateAsync(caller.TenantKey, structure, ct);

        return new MySalaryDto(
            true,
            structure.GradeCode,
            structure.GradeName,
            breakdown.Basic,
            breakdown.Allowances.Select(Line).ToList(),
            breakdown.Gross,
            breakdown.Deductions.Select(Line).ToList(),
            breakdown.TotalDeductions,
            breakdown.NetPayable,
            breakdown.Gross * 12,
            frequency,
            paymentMode,
            schedule);
    }

    public async Task<MyBankAccountDto> GetMyBankAccountAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var settings = await appSettings.GetAsync(caller.TenantKey, ct);
        RequireEnabled(settings.ShowBankDetails, "Bank Details");

        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var account = await bankAccounts.GetEmployeeAccountAsync(caller.TenantKey, employee.Id, ct);

        return account is null
            ? new MyBankAccountDto(false, null, null, null, null, null, null)
            : new MyBankAccountDto(
                true,
                account.BankName,
                account.AccountHolderName,
                $"•••• {account.AccountLast4}",
                account.Ifsc,
                account.AccountType,
                account.UpdatedUtc);
    }

    public async Task<MyOnboardingDto> GetMyOnboardingAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var settings = await appSettings.GetAsync(caller.TenantKey, ct);
        RequireEnabled(settings.ShowOnboarding, "Employee Onboarding");

        var employee = await RequireOwnEmployeeAsync(caller, ct);
        var none = new MyOnboardingDto(false, employee.JobTitle, null, null, 0, 0, [], null);

        // Only people hired through recruitment have an onboarding checklist.
        if (employee.CandidateId is not { } candidateId)
        {
            return none;
        }

        var progress = await onboarding.GetCandidateProgressAsync(caller.TenantKey, candidateId, ct);

        if (progress.Onboarding is not { } plan)
        {
            return none;
        }

        var tasks = plan.Tasks
            .Select(task =>
            {
                var status = $"{task.Status}";
                return new MyOnboardingTaskDto(
                    task.Title,
                    task.Instructions,
                    $"{task.OwnerKind}",
                    task.IsRequired,
                    status,
                    ToneFor(status));
            })
            .ToList();

        return new MyOnboardingDto(
            true,
            plan.JobTitle,
            $"{plan.Stage}",
            plan.JoiningOn,
            (int)plan.CompletionPercent,
            plan.OutstandingRequiredCount,
            tasks,
            plan.CompletedUtc);
    }

    // ---- Helpers ----------------------------------------------------------

    private static MySalaryLineDto Line(SalaryLineDto line) => new(line.Code, line.Name, line.Amount, line.Note);

    private static string ToneFor(string status)
    {
        var value = status.ToLowerInvariant();

        if (value.Contains("reject") || value.Contains("overdue"))
        {
            return "danger";
        }

        if (value.Contains("complete") || value.Contains("approved") || value.Contains("verified") || value.Contains("done"))
        {
            return "success";
        }

        if (value.Contains("waive") || value.Contains("skip"))
        {
            return "muted";
        }

        if (value.Contains("submit") || value.Contains("review"))
        {
            return "info";
        }

        return "pending";
    }

    private static void RequireEnabled(bool enabled, string feature)
    {
        if (!enabled)
        {
            throw new MobileValidationException(
                "FEATURE_DISABLED", $"{feature} has been turned off for the employee app by HR.");
        }
    }

    private async Task<VO.Employee.EmployeeRecord> RequireOwnEmployeeAsync(MobileCallerContext caller, CancellationToken ct) =>
        await employees.GetByAccountAsync(caller.TenantKey, caller.UserId, ct)
        ?? throw new MobileValidationException(
            "NO_EMPLOYEE_RECORD",
            "This account is not linked to an employee record. Ask HR to check it.");
}
