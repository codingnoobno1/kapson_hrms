using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Admin;
using Vispl.HRManagement.VO.Employee;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Admin;

/// <summary>
/// Generate Employee Salary (scopedocs/web-portal/08 §6–9, 10 §3–8).
///
/// Pay is prorated by attendance: an employee is paid for every calendar day of the month except
/// days recorded Absent and days before they joined. Unrecorded days count as paid — no record is
/// not evidence of absence. Work Days shows working days (not Sundays, weekly offs or holidays)
/// worked out of working days expected.
/// </summary>
public sealed class PayrollService(
    IPayrollRepository payroll,
    IEmployeeRepository employees,
    IAttendanceRepository attendance,
    IGradeSalaryService gradeSalaries,
    IMasterOptionRepository masters,
    IBankAccountService bankAccounts,
    ITenantService tenants,
    ILoanService loans,
    ITF.Shared.IExcelExporter excel) : IPayrollService
{
    private const string ViewPermission = "PAYROLL.VIEW";
    private const string ManagePermission = "PAYROLL.MANAGE";
    private const string ApprovePermission = "PAYROLL.APPROVE";
    private const string TransferPermission = "PAYROLL.TRANSFER";

    // Components with their own column; every other allowance or deduction lands in "Other".
    private const string HraCode = "HRA";
    private static readonly HashSet<string> ConveyanceCodes = new(["TRANSPORT", "CONVEYANCE"], StringComparer.OrdinalIgnoreCase);
    private const string PfCode = "PF";
    private const string EsiCode = "ESI";
    private const string TdsCode = "TDS";

    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // ---- Reads ------------------------------------------------------------

    public async Task<PayrollWorkspaceDto> GetWorkspaceAsync(
        MobileCallerContext caller, PayrollScopeQuery scope, CancellationToken cancellationToken = default)
    {
        Require(caller, ViewPermission);
        ValidateScope(scope);

        var context = await LoadAsync(caller.TenantKey, scope, cancellationToken);
        var rows = context.Rows;

        var divisions = rows
            .GroupBy(row => row.DivisionCode, StringComparer.OrdinalIgnoreCase)
            .Select(group => new DivisionPayrollRowDto(
                group.Key,
                group.First().DivisionName,
                group.Count(),
                group.Select(row => row.DepartmentCode).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                group.Count(IsPending),
                PayrollAmountsDto.Sum(group.Select(row => row.Amounts)),
                GroupStatus(group)))
            .OrderBy(row => row.DivisionName)
            .ToList();

        var departmentGroups = rows
            .GroupBy(row => row.DivisionCode, StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.First().DivisionName)
            .Select(division =>
            {
                var departments = division
                    .GroupBy(row => row.DepartmentCode, StringComparer.OrdinalIgnoreCase)
                    .Select(group => new DepartmentPayrollRowDto(
                        group.Key,
                        group.First().DepartmentName,
                        division.Key,
                        group.First().DivisionName,
                        group.Count(),
                        group.Count(IsPending),
                        PayrollAmountsDto.Sum(group.Select(row => row.Amounts)),
                        GroupStatus(group)))
                    .OrderBy(row => row.DepartmentName)
                    .ToList();

                return new DivisionPayrollGroupDto(
                    division.Key,
                    division.First().DivisionName,
                    departments,
                    PayrollAmountsDto.Sum(departments.Select(row => row.Totals)));
            })
            .ToList();

        var pending = rows.Count(IsPending);
        var summary = new PayrollSummaryDto(
            rows.Count,
            divisions.Count,
            rows.Sum(row => row.Amounts.Gross),
            pending,
            rows.Count - pending);

        var tenant = await tenants.GetByKeyAsync(caller.TenantKey, cancellationToken);
        var companyAccounts = await bankAccounts.GetCompanyAccountsAsync(caller, cancellationToken);

        return new PayrollWorkspaceDto(
            scope.Month,
            scope.Year,
            new DateTime(scope.Year, scope.Month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture),
            scope.SalaryType,
            tenant?.DisplayName ?? caller.TenantKey,
            new PayrollRunStateDto(
                context.Run?.Id.ToString(),
                context.Run?.ApprovedUtc is not null,
                context.Run?.GeneratedUtc,
                context.Run?.ApprovedUtc),
            summary,
            divisions,
            PayrollAmountsDto.Sum(rows.Select(row => row.Amounts)),
            departmentGroups,
            rows.OrderBy(row => row.DivisionName).ThenBy(row => row.DepartmentName).ThenBy(row => row.DisplayName).ToList(),
            rows.Count(row => row.Status == PayrollLineStatuses.Transferred),
            companyAccounts.Where(account => account.IsActive).ToList());
    }

    // ---- Generate and approve ---------------------------------------------

    public async Task<PayrollRunResultDto> GenerateAsync(
        MobileCallerContext caller, GeneratePayrollRequest request, CancellationToken cancellationToken = default)
    {
        Require(caller, ManagePermission);
        ValidateScope(request.Scope);

        var context = await LoadAsync(caller.TenantKey, request.Scope, cancellationToken);
        if (context.Run?.ApprovedUtc is { } approved)
        {
            throw new MobileValidationException(
                "PAYROLL_APPROVED", $"{MonthLabel(request.Scope)} payroll was approved on {approved:dd MMM yyyy} and can no longer be regenerated.");
        }

        var requested = request.EmployeeIds.Select(ParseId).ToHashSet();

        // No selection generates everyone still pending; a selection may also regenerate a line that
        // is only Generated. A paid or transferred salary is never recalculated.
        var targets = context.Calculations
            .Where(item => requested.Count == 0
                ? item.Line is null
                : requested.Contains(item.Employee.Id) && item.Line?.Status is null or PayrollLineStatuses.Generated)
            .ToList();

        var skipped = targets
            .Where(item => item.PendingReason is not null)
            .Select(item => $"{item.Employee.DisplayName} ({item.Employee.EmployeeNumber}): {item.PendingReason}")
            .ToList();

        var ready = targets.Where(item => item.PendingReason is null).ToList();
        var now = DateTimeOffset.UtcNow;

        var run = await payroll.GetOrCreateRunAsync(new PayrollRunRecord
        {
            TenantKey = caller.TenantKey,
            Year = request.Scope.Year,
            Month = request.Scope.Month,
            SalaryType = request.Scope.SalaryType,
            GeneratedUtc = now,
            GeneratedBy = caller.UserId
        }, cancellationToken);

        await payroll.UpsertLinesAsync(ready.Select(item => ToLine(caller, run.Id, item, now)).ToList(), cancellationToken);

        return new PayrollRunResultDto(run.Id.ToString(), ready.Count, skipped.Count, skipped);
    }

    public async Task ApproveAsync(
        MobileCallerContext caller, PayrollScopeQuery scope, CancellationToken cancellationToken = default)
    {
        Require(caller, ApprovePermission);
        ValidateScope(scope);

        // Approval locks the whole month, so it is judged on the whole month, not the filtered view.
        var whole = new PayrollScopeQuery { Month = scope.Month, Year = scope.Year, SalaryType = scope.SalaryType };
        var context = await LoadAsync(caller.TenantKey, whole, cancellationToken);

        if (context.Run is not { } run || context.Rows.All(IsPending))
        {
            throw new MobileValidationException("PAYROLL_NOT_GENERATED", $"Generate {MonthLabel(scope)} payroll before approving it.");
        }

        if (run.ApprovedUtc is not null)
        {
            throw new MobileValidationException("PAYROLL_APPROVED", $"{MonthLabel(scope)} payroll is already approved.");
        }

        var pending = context.Rows.Count(IsPending);
        if (pending > 0)
        {
            throw new MobileValidationException(
                "PAYROLL_PENDING", $"{pending} employee(s) are still pending for {MonthLabel(scope)}. Generate them before approving — approval locks the month.");
        }

        await payroll.ApproveRunAsync(caller.TenantKey, run.Id, caller.UserId, DateTimeOffset.UtcNow, cancellationToken);

        // Approval is when a month's loan deductions become real: record each as a repayment.
        // Idempotent per loan per month, so a retried approval cannot recover twice.
        var loanRecoveries = context.Calculations
            .Where(item => item.Line is not null)
            .SelectMany(item => LoanRecoveriesIn(item.Line!.BreakdownJson))
            .ToList();

        if (loanRecoveries.Count > 0)
        {
            await loans.RecordPayrollRecoveriesAsync(
                caller.TenantKey, run.Id, scope.Year, scope.Month, loanRecoveries, caller.UserId, cancellationToken);
        }
    }

    // ---- Payment ----------------------------------------------------------

    public async Task<MarkSalaryPaidResultDto> MarkPaidAsync(
        MobileCallerContext caller, MarkSalaryPaidRequest request, CancellationToken cancellationToken = default)
    {
        Require(caller, ManagePermission);
        ValidateScope(request.Scope);

        var run = await RequireApprovedRunAsync(caller.TenantKey, request.Scope, cancellationToken);
        var ids = RequireSelection(request.EmployeeIds);

        var changed = await payroll.RecordPaymentAsync(
            caller.TenantKey, run.Id, ids, PayrollLineStatuses.Paid, request.PaymentDate,
            Trimmed(request.PaymentMode, "Bank Transfer"), Trimmed(request.DocumentType, "Monthly Payroll"),
            null, caller.UserId, DateTimeOffset.UtcNow, cancellationToken);

        if (changed.Count == 0)
        {
            throw new MobileValidationException("NOTHING_TO_PAY", "None of the selected salaries are generated and unpaid.");
        }

        return new MarkSalaryPaidResultDto(changed.Count, changed.Sum(line => line.NetPayable));
    }

    public async Task<BankTransferResultDto> ExecuteTransferAsync(
        MobileCallerContext caller, ExecuteBankTransferRequest request, CancellationToken cancellationToken = default)
    {
        var (run, lines, account, _) = await PrepareTransferAsync(caller, request, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var reference = $"TXN-{now:yyyyMMdd}-{RandomNumberGenerator.GetInt32(100000, 1000000)}";

        var changed = await payroll.RecordPaymentAsync(
            caller.TenantKey, run.Id, lines.Select(line => line.EmployeeId).ToList(), PayrollLineStatuses.Transferred,
            request.TransferDate, request.TransferMode, "Monthly Payroll",
            new BankTransferBatchRecord
            {
                TenantKey = caller.TenantKey,
                RunId = run.Id,
                ReferenceNo = reference,
                CompanyBankAccountId = ParseId(account.Id),
                TransferDate = request.TransferDate,
                TransferMode = request.TransferMode,
                CreatedBy = caller.UserId,
                CreatedUtc = now
            },
            caller.UserId, now, cancellationToken);

        if (changed.Count == 0)
        {
            throw new MobileValidationException("NOTHING_TO_TRANSFER", "None of the selected salaries are waiting for transfer.");
        }

        return new BankTransferResultDto(
            changed[0].TransferBatchId?.ToString() ?? string.Empty,
            reference,
            changed.Sum(line => line.NetPayable),
            changed.Count);
    }

    public async Task<BankAdviceDto> BuildAdviceAsync(
        MobileCallerContext caller, ExecuteBankTransferRequest request, CancellationToken cancellationToken = default)
    {
        var (_, lines, account, payouts) = await PrepareTransferAsync(caller, request, cancellationToken);

        var ordered = lines.OrderBy(line => line.DisplayName).ToList();
        var rows = ordered
            .Select((line, index) =>
            {
                var payout = payouts[line.EmployeeId];

                // Account numbers stay text so a leading zero survives the bank's upload.
                return (IReadOnlyList<object?>)
                [
                    index + 1, line.EmployeeNumber, line.DisplayName, payout.AccountHolderName, payout.BankName,
                    payout.AccountNumber, payout.Ifsc, payout.AccountType, line.NetPayable,
                    request.TransferMode, request.TransferDate, account.AccountLabel
                ];
            })
            .ToList();

        var total = ordered.Sum(line => line.NetPayable);

        var workbook = excel.Build(new ITF.Shared.ExcelSheet(
            "Salary advice",
            $"Salary transfer advice — {MonthLabel(request.Scope)}",
            $"Debit account {account.AccountLabel} · {request.TransferMode} · transfer date {request.TransferDate:dd MMM yyyy} · {ordered.Count} employee(s)",
            [
                new("Sr No", ITF.Shared.ExcelFormat.Integer),
                new("Employee Number"),
                new("Employee Name"),
                new("Account Holder"),
                new("Bank"),
                new("Account Number"),
                new("IFSC"),
                new("Account Type"),
                new("Amount", ITF.Shared.ExcelFormat.Money),
                new("Transfer Mode"),
                new("Transfer Date", ITF.Shared.ExcelFormat.Date),
                new("Debit Account")
            ],
            rows,
            ["Total", null, null, null, null, null, null, null, total, null, null, null]));

        return new BankAdviceDto(
            $"salary-advice-{request.Scope.Year}-{request.Scope.Month:00}-{DateTime.UtcNow:HHmmss}.xlsx",
            workbook,
            ITF.Shared.ExcelContentTypes.Xlsx,
            total,
            ordered.Count);
    }

    /// <summary>Everything a transfer and its advice file both insist on, checked once.</summary>
    private async Task<(PayrollRunRecord Run, List<PayrollLineRecord> Lines, CompanyBankAccountDto Account, IReadOnlyDictionary<long, EmployeePayoutAccount> Payouts)>
        PrepareTransferAsync(MobileCallerContext caller, ExecuteBankTransferRequest request, CancellationToken cancellationToken)
    {
        Require(caller, TransferPermission);
        ValidateScope(request.Scope);

        if (!BankTransferModes.All.Contains(request.TransferMode))
        {
            throw new MobileValidationException("TRANSFER_MODE_UNKNOWN", $"Transfer mode must be one of {string.Join(", ", BankTransferModes.All)}.");
        }

        var run = await RequireApprovedRunAsync(caller.TenantKey, request.Scope, cancellationToken);
        var ids = RequireSelection(request.EmployeeIds);

        var account = (await bankAccounts.GetCompanyAccountsAsync(caller, cancellationToken))
            .FirstOrDefault(item => item.Id == request.CompanyBankAccountId && item.IsActive)
            ?? throw new MobileValidationException("COMPANY_ACCOUNT_UNKNOWN", "Choose an active company bank account to transfer from.");

        var lines = (await payroll.GetLinesAsync(caller.TenantKey, run.Id, cancellationToken))
            .Where(line => ids.Contains(line.EmployeeId) && line.Status == PayrollLineStatuses.Generated)
            .ToList();

        if (lines.Count == 0)
        {
            throw new MobileValidationException("NOTHING_TO_TRANSFER", "None of the selected salaries are waiting for transfer.");
        }

        // A missing account must block the transfer, never send money to nowhere (doc 10 §8).
        var payouts = await bankAccounts.GetPayoutAccountsAsync(caller.TenantKey, cancellationToken);
        var missing = lines.Where(line => !payouts.ContainsKey(line.EmployeeId)).Select(line => line.DisplayName).ToList();
        if (missing.Count > 0)
        {
            throw new MobileValidationException(
                "BANK_ACCOUNT_MISSING", $"No salary bank account for: {string.Join(", ", missing)}. Add one on Bank Accounts first.");
        }

        return (run, lines, account, payouts);
    }

    // ---- Calculation ------------------------------------------------------

    private sealed record Calculation(
        EmployeeRecord Employee,
        PayrollLineRecord? Line,
        EmployeePayrollRowDto Row,
        string? PendingReason,
        SalaryBreakdownDto? Breakdown,
        int PayableDays,
        int DaysInMonth);

    private sealed record PayrollContext(
        PayrollRunRecord? Run,
        IReadOnlyList<Calculation> Calculations)
    {
        public IReadOnlyList<EmployeePayrollRowDto> Rows { get; } = Calculations.Select(item => item.Row).ToList();
    }

    private async Task<PayrollContext> LoadAsync(string tenantKey, PayrollScopeQuery scope, CancellationToken cancellationToken)
    {
        var first = new DateOnly(scope.Year, scope.Month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        var run = await payroll.GetRunAsync(tenantKey, scope.Year, scope.Month, scope.SalaryType, cancellationToken);
        var lines = run is null
            ? new Dictionary<long, PayrollLineRecord>()
            : (await payroll.GetLinesAsync(tenantKey, run.Id, cancellationToken)).ToDictionary(line => line.EmployeeId);

        // Everyone active who had started by the month's end, plus anyone already holding a line
        // for the month even if they have since left — a paid month does not vanish.
        var people = (await employees.QueryAsync(
                tenantKey, new EmployeeQueryFilter(null, null, null, null, null, JoinedOnOrBefore: last), cancellationToken))
            .Where(person => person.IsActive || lines.ContainsKey(person.Id))
            .ToList();

        var options = await masters.GetAllAsync(tenantKey, cancellationToken);
        var divisionNames = NamesFor(options, MasterCategories.Division);
        var departmentNames = NamesFor(options, MasterCategories.Department);

        var structures = (await gradeSalaries.GetAllAsync(tenantKey, cancellationToken))
            .ToDictionary(structure => structure.GradeCode, StringComparer.OrdinalIgnoreCase);

        var days = people.Count == 0
            ? []
            : await attendance.QueryDaysAsync(
                tenantKey, new AttendanceDayFilter(first, last, null, people.Select(person => person.Id).ToList(), null, false), cancellationToken);
        var daysByEmployee = days.GroupBy(day => day.EmployeeId).ToDictionary(group => group.Key, group => group.ToList());

        var summaries = await bankAccounts.GetSummariesAsync(tenantKey, cancellationToken);
        var recoveries = await loans.GetDueRecoveriesAsync(tenantKey, scope.Year, scope.Month, cancellationToken);

        var calculations = new List<Calculation>();
        foreach (var person in people)
        {
            lines.TryGetValue(person.Id, out var line);

            var divisionCode = line?.DivisionCode ?? person.DivisionCode ?? string.Empty;
            var departmentCode = line?.DepartmentCode ?? person.DepartmentCode;

            if (!Matches(scope.DivisionCode, divisionCode) || !Matches(scope.DepartmentCode, departmentCode))
            {
                continue;
            }

            daysByEmployee.TryGetValue(person.Id, out var personDays);
            var calculation = Calculate(person, line, personDays ?? [], first, last, structures, recoveries.GetValueOrDefault(person.Id));

            var amounts = line is not null ? AmountsOf(line) : calculation.Amounts;
            var row = new EmployeePayrollRowDto(
                person.Id.ToString(),
                person.EmployeeNumber,
                line?.DisplayName ?? person.DisplayName,
                divisionCode,
                NameOr(divisionNames, divisionCode, "Unassigned"),
                departmentCode,
                NameOr(departmentNames, departmentCode, "Unassigned"),
                line?.Designation ?? person.JobTitle ?? "—",
                line?.ShiftCode ?? calculation.ShiftCode,
                line?.WorkedDays ?? calculation.WorkedDays,
                line?.ExpectedDays ?? calculation.ExpectedDays,
                amounts,
                line?.Status ?? PayrollLineStatuses.Pending,
                line is null ? calculation.PendingReason : null,
                summaries.GetValueOrDefault(person.Id),
                line?.PaidOn);

            calculations.Add(new Calculation(
                person, line, row, calculation.PendingReason, calculation.Breakdown, calculation.PayableDays, calculation.DaysInMonth));
        }

        return new PayrollContext(run, calculations);
    }

    private sealed record Figures(
        PayrollAmountsDto Amounts,
        SalaryBreakdownDto? Breakdown,
        string? PendingReason,
        string? ShiftCode,
        int WorkedDays,
        int ExpectedDays,
        int PayableDays,
        int DaysInMonth);

    private static Figures Calculate(
        EmployeeRecord person,
        PayrollLineRecord? line,
        IReadOnlyList<AttendanceDayRecord> days,
        DateOnly first,
        DateOnly last,
        IReadOnlyDictionary<string, GradeSalaryStructureDto> structures,
        IReadOnlyList<LoanRecoveryDto>? loanRecoveries)
    {
        var daysInMonth = last.Day;
        var start = person.JoinedOn > first ? person.JoinedOn : first;
        var byDate = days.GroupBy(day => day.WorkDate).ToDictionary(group => group.Key, group => group.First());

        int expected = 0, worked = 0, absentCalendarDays = 0;
        for (var date = first; date <= last; date = date.AddDays(1))
        {
            byDate.TryGetValue(date, out var day);
            var isOff = day?.Status is "WeeklyOff" or "Holiday" || (day is null && date.DayOfWeek == DayOfWeek.Sunday);
            var isAbsent = day?.Status == "Absent";

            if (date >= start && isAbsent)
            {
                absentCalendarDays++;
            }

            if (isOff)
            {
                continue;
            }

            expected++;
            if (date >= start && !isAbsent)
            {
                worked++;
            }
        }

        var beforeJoining = start.DayNumber - first.DayNumber;
        var payableDays = Math.Max(0, daysInMonth - beforeJoining - absentCalendarDays);

        var shift = days
            .Where(day => !string.IsNullOrWhiteSpace(day.ShiftCode))
            .GroupBy(day => day.ShiftCode!)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();

        string? reason = null;
        GradeSalaryStructureDto? structure = null;

        if (string.IsNullOrWhiteSpace(person.GradeCode))
        {
            reason = "No grade assigned";
        }
        else if (!structures.TryGetValue(person.GradeCode, out structure) || !structure.IsConfigured)
        {
            reason = $"No salary structure for grade {person.GradeCode}";
        }

        if (structure is null || reason is not null || line is not null)
        {
            return new Figures(PayrollAmountsDto.Zero, null, reason, shift, worked, expected, payableDays, daysInMonth);
        }

        var factor = (decimal)payableDays / daysInMonth;
        var prorated = new GradeSalaryStructureDto
        {
            GradeCode = structure.GradeCode,
            GradeName = structure.GradeName,
            BasicMonthly = structure.BasicMonthly * factor,
            IsConfigured = true,
            // Earnings scale with days paid; a fixed deduction such as professional tax does not.
            Components = structure.Components.Select(component => new GradeSalaryComponentDto
            {
                Kind = component.Kind,
                ComponentCode = component.ComponentCode,
                ComponentName = component.ComponentName,
                CalculationType = component.CalculationType,
                Value = component.Kind == SalaryComponentKinds.Allowance && component.CalculationType == SalaryCalculationTypes.Fixed
                    ? component.Value * factor
                    : component.Value,
                CapAmount = component.CapAmount,
                GrossCeiling = component.GrossCeiling
            }).ToList()
        };

        var names = structure.Components
            .GroupBy(component => $"{component.Kind}:{component.ComponentCode}", StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().ComponentName ?? group.First().ComponentCode, StringComparer.OrdinalIgnoreCase);

        var breakdown = WithLoanRecoveries(SalaryCalculator.Calculate(prorated, names), loanRecoveries);
        return new Figures(ToAmounts(breakdown), breakdown, null, shift, worked, expected, payableDays, daysInMonth);
    }

    private static PayrollAmountsDto ToAmounts(SalaryBreakdownDto breakdown)
    {
        decimal Allowance(Func<string, bool> match) => breakdown.Allowances.Where(line => match(line.Code)).Sum(line => line.Amount);
        decimal Deduction(Func<string, bool> match) => breakdown.Deductions.Where(line => match(line.Code)).Sum(line => line.Amount);

        var hra = Allowance(code => Is(code, HraCode));
        var conveyance = Allowance(ConveyanceCodes.Contains);
        var otherAllowances = breakdown.Gross - breakdown.Basic - hra - conveyance;

        var pf = Deduction(code => Is(code, PfCode));
        var esi = Deduction(code => Is(code, EsiCode));
        var tds = Deduction(code => Is(code, TdsCode));
        var otherDeductions = breakdown.TotalDeductions - pf - esi - tds;

        return new PayrollAmountsDto(
            breakdown.Basic, hra, conveyance, otherAllowances, breakdown.Gross,
            pf, esi, tds, otherDeductions, breakdown.TotalDeductions, breakdown.NetPayable);
    }

    private static PayrollAmountsDto AmountsOf(PayrollLineRecord line) => new(
        line.Basic, line.Hra, line.Conveyance, line.OtherAllowances, line.Gross,
        line.Pf, line.Esi, line.Tds, line.OtherDeductions, line.Deductions, line.NetPayable);

    private static PayrollLineRecord ToLine(MobileCallerContext caller, long runId, Calculation item, DateTimeOffset now)
    {
        var amounts = ToAmounts(item.Breakdown!);

        return new PayrollLineRecord
        {
            TenantKey = caller.TenantKey,
            RunId = runId,
            EmployeeId = item.Employee.Id,
            EmployeeNumber = item.Employee.EmployeeNumber,
            DisplayName = item.Employee.DisplayName,
            DivisionCode = item.Row.DivisionCode,
            DepartmentCode = item.Row.DepartmentCode,
            Designation = item.Row.Designation,
            GradeCode = item.Employee.GradeCode!,
            ShiftCode = item.Row.ShiftCode,
            WorkedDays = item.Row.WorkedDays,
            ExpectedDays = item.Row.ExpectedDays,
            PayableDays = item.PayableDays,
            DaysInMonth = item.DaysInMonth,
            Basic = amounts.Basic,
            Hra = amounts.Hra,
            Conveyance = amounts.Conveyance,
            OtherAllowances = amounts.OtherAllowances,
            Gross = amounts.Gross,
            Pf = amounts.Pf,
            Esi = amounts.Esi,
            Tds = amounts.Tds,
            OtherDeductions = amounts.OtherDeductions,
            Deductions = amounts.Deductions,
            NetPayable = amounts.NetPayable,
            BreakdownJson = JsonSerializer.Serialize(item.Breakdown, Json),
            Status = PayrollLineStatuses.Generated,
            UpdatedBy = caller.UserId,
            UpdatedUtc = now
        };
    }

    // ---- Loan recovery ------------------------------------------------------

    /// <summary>Breakdown line code for a loan recovery; the loan id follows the prefix.</summary>
    private const string LoanRecoveryPrefix = "LOAN:";

    /// <summary>
    /// Adds each due instalment as a deduction, never taking net pay below zero. What cannot be
    /// deducted this month stays outstanding on the loan and is due again next month.
    /// </summary>
    private static SalaryBreakdownDto WithLoanRecoveries(SalaryBreakdownDto breakdown, IReadOnlyList<LoanRecoveryDto>? recoveries)
    {
        if (recoveries is null || recoveries.Count == 0)
        {
            return breakdown;
        }

        var deductions = breakdown.Deductions.ToList();
        var available = breakdown.NetPayable;

        foreach (var recovery in recoveries.OrderBy(item => item.LoanId))
        {
            var name = recovery.Type == LoanTypes.Advance ? "Salary advance recovery" : "Loan recovery";
            var amount = Math.Min(recovery.Amount, Math.Max(0, available));

            deductions.Add(new SalaryLineDto(
                $"{LoanRecoveryPrefix}{recovery.LoanId}",
                name,
                amount,
                amount < recovery.Amount ? $"{recovery.VoucherNo} — only {SalaryCalculator.Rupees(amount)} of {SalaryCalculator.Rupees(recovery.Amount)} fitted in net pay" : recovery.VoucherNo));

            available -= amount;
        }

        var total = deductions.Sum(line => line.Amount);
        return breakdown with { Deductions = deductions, TotalDeductions = total, NetPayable = breakdown.Gross - total };
    }

    private static IEnumerable<(long LoanId, decimal Amount)> LoanRecoveriesIn(string breakdownJson)
    {
        SalaryBreakdownDto? breakdown;
        try
        {
            breakdown = JsonSerializer.Deserialize<SalaryBreakdownDto>(breakdownJson, Json);
        }
        catch (JsonException)
        {
            yield break;
        }

        foreach (var line in breakdown?.Deductions ?? [])
        {
            if (line.Amount > 0 &&
                line.Code.StartsWith(LoanRecoveryPrefix, StringComparison.Ordinal) &&
                long.TryParse(line.Code[LoanRecoveryPrefix.Length..], out var loanId))
            {
                yield return (loanId, line.Amount);
            }
        }
    }

    // ---- Helpers ----------------------------------------------------------

    private async Task<PayrollRunRecord> RequireApprovedRunAsync(string tenantKey, PayrollScopeQuery scope, CancellationToken cancellationToken)
    {
        var run = await payroll.GetRunAsync(tenantKey, scope.Year, scope.Month, scope.SalaryType, cancellationToken);
        return run?.ApprovedUtc is not null
            ? run
            : throw new MobileValidationException(
                "PAYROLL_NOT_APPROVED", $"{MonthLabel(scope)} payroll must be approved (Preview → Approve & Proceed) before salaries are paid.");
    }

    private static HashSet<long> RequireSelection(IReadOnlyList<string> employeeIds)
    {
        var ids = employeeIds.Select(ParseId).ToHashSet();
        return ids.Count > 0 ? ids : throw new MobileValidationException("NOTHING_SELECTED", "Select at least one employee.");
    }

    private static void ValidateScope(PayrollScopeQuery scope)
    {
        if (scope.Month is < 1 or > 12 || scope.Year is < 2000 or > 2100)
        {
            throw new MobileValidationException("SCOPE_INVALID", "Choose a valid month and year.");
        }

        if (!string.Equals(scope.SalaryType, PayrollSalaryTypes.Monthly, StringComparison.OrdinalIgnoreCase))
        {
            throw new MobileValidationException("SALARY_TYPE_UNKNOWN", "Only monthly salary is supported.");
        }

        scope.SalaryType = PayrollSalaryTypes.Monthly;
    }

    private static bool IsPending(EmployeePayrollRowDto row) => row.Status == PayrollLineStatuses.Pending;

    private static string GroupStatus(IEnumerable<EmployeePayrollRowDto> rows) =>
        rows.Any(IsPending) ? PayrollLineStatuses.Pending : PayrollLineStatuses.Generated;

    private static bool Matches(string? filter, string value) =>
        string.IsNullOrWhiteSpace(filter) || string.Equals(filter, value, StringComparison.OrdinalIgnoreCase);

    private static bool Is(string code, string expected) => string.Equals(code, expected, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> NamesFor(IReadOnlyList<MasterOptionRecord> options, string category) =>
        options
            .Where(option => option.Category == category)
            .GroupBy(option => option.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

    // Some older employee rows hold a department name rather than a code; show it as it is.
    private static string NameOr(IReadOnlyDictionary<string, string> names, string code, string fallback) =>
        string.IsNullOrWhiteSpace(code) ? fallback : names.GetValueOrDefault(code) ?? code;

    private static string MonthLabel(PayrollScopeQuery scope) =>
        new DateTime(scope.Year, scope.Month, 1).ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    private static string Trimmed(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static long ParseId(string? value) =>
        long.TryParse(value, out var id) && id > 0
            ? id
            : throw new MobileNotFoundException("That record no longer exists.");

    private static void Require(MobileCallerContext caller, string permission)
    {
        if (!caller.Has(permission))
        {
            throw new MobileAccessDeniedException(permission);
        }
    }
}
