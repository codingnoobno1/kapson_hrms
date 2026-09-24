namespace Vispl.HRManagement.Contracts.Mobile.Home;

/// <summary>
/// What HR has switched on for the employee app (HR portal → Settings → Employee App). The app
/// hides what is off; the server refuses it regardless, so hiding is a courtesy, not the control.
/// </summary>
public sealed record EmployeeAppFeaturesDto(
    bool AllowAppPunch,
    bool RequireLocationForPunch,
    bool AllowGatePassRequests,
    bool AllowAttendanceCorrections,
    bool ShowSalaryDetails,
    bool ShowBankDetails,
    bool ShowOnboarding,
    bool AllowProfileChangeRequests,
    int AttendanceTargetPercent = 85);

public sealed record MySalaryLineDto(string Code, string Name, decimal Amount, string? Note);

/// <summary>
/// The Salary Details sheet: the caller's grade structure, calculated exactly as payroll
/// calculates it. It is the standard month — what is actually paid depends on attendance.
/// </summary>
public sealed record MySalaryDto(
    bool IsConfigured,
    string? GradeCode,
    string? GradeName,
    decimal BasicMonthly,
    IReadOnlyList<MySalaryLineDto> Allowances,
    decimal GrossMonthly,
    IReadOnlyList<MySalaryLineDto> Deductions,
    decimal TotalDeductionsMonthly,
    decimal NetMonthly,
    decimal AnnualGross,
    string PaymentFrequency,
    string PaymentMode,
    string PaymentSchedule);

/// <summary>The Bank Details sheet. The account number only ever leaves the server masked.</summary>
public sealed record MyBankAccountDto(
    bool HasAccount,
    string? BankName,
    string? AccountHolderName,
    string? MaskedAccountNumber,
    string? Ifsc,
    string? AccountType,
    DateTimeOffset? UpdatedUtc);

public sealed record MyOnboardingTaskDto(
    string Title,
    string? Instructions,
    string Owner,
    bool IsRequired,
    string Status,
    string Tone);

/// <summary>The Employee Onboarding sheet, for an employee who joined through recruitment.</summary>
public sealed record MyOnboardingDto(
    bool HasOnboarding,
    string? JobTitle,
    string? Stage,
    DateOnly? JoiningOn,
    int CompletionPercent,
    int OutstandingRequiredCount,
    IReadOnlyList<MyOnboardingTaskDto> Tasks,
    DateTimeOffset? CompletedUtc);
