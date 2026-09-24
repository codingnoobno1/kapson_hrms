using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Contracts.Mobile.Payroll;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Payslips for the signed-in employee, read from approved payroll runs.
///
/// Only approved runs are visible. A generated-but-unapproved run is working state — showing it
/// would present figures that can still change as though they were final pay. Whether salary is
/// visible in the app at all is HR's switch, checked on every read rather than assumed from the
/// permission alone.
/// </summary>
public sealed class MobilePayrollService(
    IPayrollRepository payroll,
    IEmployeeRepository employees,
    IGradeSalaryService gradeSalaries,
    IEmployeeAppSettingsService appSettings) : IMobilePayrollService
{
    private const string Currency = "INR";
    private const string SalaryType = "Monthly";

    /// <summary>How far back the app offers payslips. Older ones are a web-console concern.</summary>
    private const int MonthsOffered = 24;

    public async Task<CursorPage<PayslipListItemDto>> ListPayslipsAsync(
        MobileCallerContext caller, PayslipListQuery query, CancellationToken ct)
    {
        var employee = await OwnRecordAsync(caller, ct);
        await RequireSalaryVisibleAsync(caller, ct);

        var items = new List<PayslipListItemDto>();
        var month = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 0; i < MonthsOffered; i++, month = month.AddMonths(-1))
        {
            if (query.Year is { } year && month.Year != year)
            {
                continue;
            }

            var run = await payroll.GetRunAsync(caller.TenantKey, month.Year, month.Month, SalaryType, ct);
            if (run is null || run.ApprovedUtc is null)
            {
                continue;
            }

            var line = (await payroll.GetLinesAsync(caller.TenantKey, run.Id, ct))
                .FirstOrDefault(row => row.EmployeeId == employee.Id);

            if (line is null)
            {
                continue;
            }

            items.Add(new PayslipListItemDto(
                line.Id.ToString(),
                PeriodOf(run.Year, run.Month),
                run.ApprovedUtc.Value,
                new MoneyDto(line.NetPayable, Currency),
                // The download issues a protected-file ticket, which has no implementation yet.
                IsProtectedDownloadAvailable: false));
        }

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = items.Take(pageSize).ToList();

        return new CursorPage<PayslipListItemDto>(
            page, null, items.Count > page.Count, pageSize, items.Count);
    }

    public async Task<PayslipDetailDto> GetPayslipAsync(
        MobileCallerContext caller, string payslipId, CancellationToken ct)
    {
        var employee = await OwnRecordAsync(caller, ct);
        await RequireSalaryVisibleAsync(caller, ct);

        if (!long.TryParse(payslipId, out var lineId))
        {
            throw new MobileNotFoundException("That payslip no longer exists.");
        }

        var month = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 0; i < MonthsOffered; i++, month = month.AddMonths(-1))
        {
            var run = await payroll.GetRunAsync(caller.TenantKey, month.Year, month.Month, SalaryType, ct);
            if (run is null || run.ApprovedUtc is null)
            {
                continue;
            }

            var lines = await payroll.GetLinesAsync(caller.TenantKey, run.Id, ct);
            var line = lines.FirstOrDefault(row => row.Id == lineId);

            if (line is null)
            {
                continue;
            }

            // Addressing a payslip by id must not become a way to read a colleague's pay.
            if (line.EmployeeId != employee.Id)
            {
                throw new MobileNotFoundException("That payslip no longer exists.");
            }

            return new PayslipDetailDto(
                line.Id.ToString(),
                PeriodOf(run.Year, run.Month),
                Earnings(line),
                Deductions(line),
                new MoneyDto(line.NetPayable, Currency),
                new MoneyDto(line.Gross, Currency),
                new MoneyDto(line.Tds, Currency),
                RowVersion: null);
        }

        throw new MobileNotFoundException("That payslip no longer exists.");
    }

    public async Task<SalaryStructureDto> GetSalaryStructureAsync(
        MobileCallerContext caller, string? employeeId, CancellationToken ct)
    {
        var employee = employeeId is null
            ? await OwnRecordAsync(caller, ct)
            : await OtherRecordAsync(caller, employeeId, ct);

        await RequireSalaryVisibleAsync(caller, ct);

        if (string.IsNullOrWhiteSpace(employee.GradeCode))
        {
            throw new MobileNotFoundException("No salary grade is recorded for this employee.");
        }

        var structures = await gradeSalaries.GetAllAsync(caller.TenantKey, ct);
        var structure = structures.FirstOrDefault(item =>
            string.Equals(item.GradeCode, employee.GradeCode, StringComparison.OrdinalIgnoreCase))
            ?? throw new MobileNotFoundException("No salary structure is published for this grade.");

        // Turning a grade into rupees is payroll's own calculation — percent-of-basic, caps and
        // gross ceilings all live in it. Reusing it is what stops the app quoting a figure the
        // payslip will not match.
        var breakdown = await gradeSalaries.CalculateAsync(caller.TenantKey, structure, ct);

        var components = new List<PayComponentDto>
        {
            Component("BASIC", "Basic", breakdown.Basic, "earning", taxable: true)
        };

        components.AddRange(breakdown.Allowances.Select(line =>
            Component(line.Code, line.Name, line.Amount, "earning", taxable: true)));

        components.AddRange(breakdown.Deductions.Select(line =>
            Component(line.Code, line.Name, line.Amount, "deduction", taxable: false)));

        return new SalaryStructureDto(
            // The structure is versioned by its last edit rather than an effective date, so the
            // date it started applying is the closest honest answer available.
            DateOnly.FromDateTime((structure.UpdatedUtc ?? DateTimeOffset.UtcNow).UtcDateTime),
            new MoneyDto(breakdown.Gross * 12, Currency),
            components);
    }

    // ---- Not yet backed by storage ----------------------------------------

    public Task<ProtectedFileTicketDto> GetPayslipTicketAsync(
        MobileCallerContext caller, string payslipId, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Payslip Download", "protected file ticket issuer", "payslip document renderer");

    public Task<TaxSummaryDto> GetTaxSummaryAsync(
        MobileCallerContext caller, TaxSummaryQuery query, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Tax Summary", "tax computation service", "financial year projection");

    public Task<TaxDeclarationDto> GetDeclarationAsync(
        MobileCallerContext caller, int? financialYear, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Tax Declarations", "declaration store", "investment proof workflow");

    public Task<MutationResultDto> SaveDeclarationAsync(
        MobileCallerContext caller, SaveTaxDeclarationRequest request, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Tax Declarations", "declaration store", "investment proof workflow");

    public Task<MutationResultDto> SubmitDeclarationAsync(
        MobileCallerContext caller, SubmitTaxDeclarationRequest request, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Tax Declarations", "declaration store", "investment proof workflow");

    public Task<MutationResultDto> VerifyDeclarationLineAsync(
        MobileCallerContext caller, string employeeId, VerifyTaxDeclarationLineRequest request, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Tax Declarations", "declaration store", "verifier workflow");

    // ---- Helpers ----------------------------------------------------------

    private static string PeriodOf(int year, int month) =>
        new DateOnly(year, month, 1).ToString("MMMM yyyy");

    private static PayComponentDto Component(
        string code, string name, decimal amount, string category, bool taxable) =>
        new(code, name, new MoneyDto(amount, Currency), category, taxable, null);

    private static IReadOnlyList<PayComponentDto> Earnings(PayrollLineRecord line) =>
    [
        Component("BASIC", "Basic", line.Basic, "earning", taxable: true),
        Component("HRA", "House Rent Allowance", line.Hra, "earning", taxable: true),
        Component("CONVEYANCE", "Conveyance", line.Conveyance, "earning", taxable: false),
        Component("OTHER", "Other Allowances", line.OtherAllowances, "earning", taxable: true)
    ];

    private static IReadOnlyList<PayComponentDto> Deductions(PayrollLineRecord line) =>
    [
        Component("PF", "Provident Fund", line.Pf, "deduction", taxable: false),
        Component("ESI", "Employee State Insurance", line.Esi, "deduction", taxable: false),
        Component("TDS", "Tax Deducted at Source", line.Tds, "deduction", taxable: false),
        Component("OTHER", "Other Deductions", line.OtherDeductions, "deduction", taxable: false)
    ];

    /// <summary>
    /// HR can hide pay in the app entirely. The check is a denial rather than an empty payslip,
    /// so the app can explain why instead of implying the employee was not paid.
    /// </summary>
    private async Task RequireSalaryVisibleAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var settings = await appSettings.GetAsync(caller.TenantKey, ct);
        if (!settings.ShowSalaryDetails)
        {
            throw new MobileAccessDeniedException("PAYROLL.VIEW");
        }
    }

    private async Task<VO.Employee.EmployeeRecord> OwnRecordAsync(
        MobileCallerContext caller, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(caller.EmployeeId))
        {
            throw new MobileNotFoundException("This account is not linked to an employee record.");
        }

        return await employees.GetByNumberAsync(caller.TenantKey, caller.EmployeeId, ct)
            ?? throw new MobileNotFoundException("Your employee record no longer exists.");
    }

    private async Task<VO.Employee.EmployeeRecord> OtherRecordAsync(
        MobileCallerContext caller, string employeeId, CancellationToken ct)
    {
        if (!long.TryParse(employeeId, out var id))
        {
            throw new MobileNotFoundException("That employee no longer exists.");
        }

        var person = await employees.GetAsync(caller.TenantKey, id, ct)
            ?? throw new MobileNotFoundException("That employee no longer exists.");

        var isSelf = !string.IsNullOrWhiteSpace(caller.EmployeeId) &&
                     string.Equals(person.EmployeeNumber, caller.EmployeeId, StringComparison.OrdinalIgnoreCase);

        if (!isSelf && !caller.Has("PAYROLL.VIEW"))
        {
            throw new MobileAccessDeniedException("PAYROLL.VIEW");
        }

        return person;
    }
}
