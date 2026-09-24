using System.Globalization;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.VO.Admin;
using Vispl.HRManagement.VO.Employee;

namespace Vispl.HRManagement.BM.Admin;

/// <summary>
/// Loans and salary advances.
///
/// A loan may carry flat annual interest; an advance never does. The total repayable is split into
/// equal whole-rupee instalments (the last one absorbs the rounding) starting from a chosen payroll
/// month. Payroll deducts each month's instalment and, when the month is approved, records it as a
/// repayment — once per loan per month, so regenerating a month never recovers twice. A loan closes
/// itself when nothing is left outstanding.
/// </summary>
public sealed class LoanService(
    ILoanRepository loans,
    IEmployeeRepository employees,
    IMasterOptionRepository masters) : ILoanService
{
    private const string ViewPermission = "PAYROLL.LOAN.VIEW";
    private const string ManagePermission = "PAYROLL.LOAN.MANAGE";

    // ---- Reads --------------------------------------------------------------

    public async Task<LoanWorkspaceDto> GetWorkspaceAsync(
        MobileCallerContext caller, LoanQuery query, CancellationToken cancellationToken = default)
    {
        Require(caller, ViewPermission);

        var context = await LoadAsync(caller.TenantKey, cancellationToken);
        var search = query.Search?.Trim();

        var items = context.Loans
            .Select(context.ToItem)
            .Where(item => Blank(query.Status) || Same(item.Status, query.Status))
            .Where(item => Blank(query.Type) || Same(item.Type, query.Type))
            .Where(item => Blank(query.DivisionCode) || Same(context.DivisionOf(item), query.DivisionCode))
            .Where(item => Blank(query.DepartmentCode) || Same(context.DepartmentOf(item), query.DepartmentCode))
            .Where(item => Blank(query.DesignationName) ||
                           (item.Designation?.Contains(query.DesignationName!.Trim(), StringComparison.OrdinalIgnoreCase) ?? false))
            .Where(item => Blank(query.EmployeeNumber) || item.EmployeeNumber.Contains(query.EmployeeNumber!.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(item => query.AmountMin is not { } min || item.Principal >= min)
            .Where(item => query.AmountMax is not { } max || item.Principal <= max)
            .Where(item => query.RequestedFrom is not { } from || item.RequestedOn >= from)
            .Where(item => query.RequestedTo is not { } to || item.RequestedOn <= to)
            .Where(item => string.IsNullOrEmpty(search) ||
                           item.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           item.EmployeeNumber.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           item.VoucherNo.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                           (item.DepartmentName?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var recoveredThisMonth = context.Repayments
            .Where(repayment => repayment.Year == today.Year && repayment.Month == today.Month)
            .Where(repayment => items.Any(item => item.Id == repayment.LoanId.ToString(CultureInfo.InvariantCulture)))
            .Sum(repayment => repayment.Amount);

        var loanItems = items.Where(item => item.Type == LoanTypes.Loan).ToList();
        var advanceItems = items.Where(item => item.Type == LoanTypes.Advance).ToList();

        var summary = new LoanSummaryDto(
            items.Count(item => item.Status == LoanStatuses.Active),
            items.Count(item => item.Status == LoanStatuses.Pending),
            items.Where(item => item.Status is LoanStatuses.Active or LoanStatuses.Closed).Sum(item => item.Principal),
            items.Where(item => item.Status == LoanStatuses.Active).Sum(item => item.Outstanding),
            recoveredThisMonth,
            loanItems.Where(item => item.Status is LoanStatuses.Active or LoanStatuses.Closed).Sum(item => item.Principal),
            loanItems.Where(item => item.Status == LoanStatuses.Active).Sum(item => item.Outstanding),
            loanItems.Sum(item => item.Recovered),
            advanceItems.Where(item => item.Status is LoanStatuses.Active or LoanStatuses.Closed).Sum(item => item.Principal),
            advanceItems.Where(item => item.Status is LoanStatuses.Active or LoanStatuses.Pending).Sum(item => item.Outstanding),
            items.Select(item => item.EmployeeId).Distinct().Count());

        var summaryRows = items
            .GroupBy(item => item.EmployeeId)
            .Select(group =>
            {
                var first = group.First();
                var groupLoans = group.Where(item => item.Type == LoanTypes.Loan).ToList();
                var groupAdvances = group.Where(item => item.Type == LoanTypes.Advance).ToList();

                return new LoanSummaryRowDto(
                    first.EmployeeId,
                    first.EmployeeNumber,
                    first.DisplayName,
                    first.DepartmentName,
                    groupLoans.Sum(item => item.Principal),
                    groupLoans.Where(item => item.Status == LoanStatuses.Active).Sum(item => item.Outstanding),
                    groupAdvances.Where(item => item.Status is LoanStatuses.Active or LoanStatuses.Pending).Sum(item => item.Outstanding),
                    group.Sum(item => item.MonthlyInstallment),
                    LoanStatusOf(groupLoans),
                    LoanStatusOf(groupAdvances));
            })
            .OrderBy(row => row.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new LoanWorkspaceDto(summary, items, summaryRows);
    }

    /// <summary>The one status shown for a person's group of loans (or advances): the most active wins.</summary>
    private static string LoanStatusOf(IReadOnlyList<LoanListItemDto> group)
    {
        if (group.Count == 0)
        {
            return "—";
        }

        if (group.Any(item => item.Status == LoanStatuses.Active))
        {
            return LoanStatuses.Active;
        }

        if (group.Any(item => item.Status == LoanStatuses.Pending))
        {
            return LoanStatuses.Pending;
        }

        return group[0].Status;
    }

    public async Task<LoanLedgerDto> GetLedgerAsync(
        MobileCallerContext caller, string loanId, CancellationToken cancellationToken = default)
    {
        Require(caller, ViewPermission);

        var detail = await GetDetailAsync(caller, loanId, cancellationToken);
        var context = await LoadAsync(caller.TenantKey, cancellationToken);
        var person = context.PersonOf(detail.Loan);

        var rows = new List<LoanLedgerRowDto>();
        var balance = detail.Loan.TotalRepayable;

        foreach (var step in detail.Schedule)
        {
            var opening = balance;
            var paid = Math.Min(step.Recovered, opening);
            balance = Math.Max(0, opening - paid);

            rows.Add(new LoanLedgerRowDto(
                step.Number, step.MonthLabel, opening, step.Due, paid, balance,
                paid >= step.Due && step.Due > 0 ? "Active" : step.State == "Overdue" ? "Overdue" : "Active"));
        }

        return new LoanLedgerDto(detail.Loan, person?.JobTitle, rows);
    }

    public async Task<LoanDetailDto> GetDetailAsync(
        MobileCallerContext caller, string loanId, CancellationToken cancellationToken = default)
    {
        Require(caller, ViewPermission);

        var context = await LoadAsync(caller.TenantKey, cancellationToken);
        var loan = context.Loans.FirstOrDefault(item => item.Id == ParseId(loanId))
            ?? throw new MobileNotFoundException("That loan no longer exists.");

        var repayments = context.RepaymentsOf(loan.Id);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var currentIndex = today.Year * 12 + today.Month;

        var schedule = new List<LoanScheduleRowDto>();
        decimal plannedSoFar = 0, recoveredSoFar = 0;

        for (var number = 1; number <= loan.Installments; number++)
        {
            var (year, month) = AddMonths(loan.StartYear, loan.StartMonth, number - 1);
            var due = number == loan.Installments
                ? loan.TotalRepayable - loan.MonthlyInstallment * (loan.Installments - 1)
                : loan.MonthlyInstallment;

            var recovered = repayments.Where(item => item.Year == year && item.Month == month).Sum(item => item.Amount);
            plannedSoFar += due;
            recoveredSoFar += recovered;

            var state = recoveredSoFar >= plannedSoFar ? "Recovered"
                : loan.Status != LoanStatuses.Active ? "—"
                : year * 12 + month < currentIndex ? "Overdue"
                : year * 12 + month == currentIndex ? "Due"
                : "Upcoming";

            schedule.Add(new LoanScheduleRowDto(
                number, year, month, MonthLabel(year, month), Math.Max(0, due), recovered,
                Math.Max(0, loan.TotalRepayable - Math.Max(plannedSoFar, recoveredSoFar)), state));
        }

        return new LoanDetailDto(
            context.ToItem(loan),
            schedule,
            repayments
                .OrderByDescending(item => item.RecordedOn).ThenByDescending(item => item.Id)
                .Select(item => new LoanRepaymentDto(
                    item.Id.ToString(CultureInfo.InvariantCulture), item.Year, item.Month, MonthLabel(item.Year, item.Month),
                    item.Amount, item.Source, item.RecordedOn, item.Note))
                .ToList());
    }

    // ---- Writes -------------------------------------------------------------

    public async Task<LoanListItemDto> CreateAsync(
        MobileCallerContext caller, SaveLoanRequest request, CancellationToken cancellationToken = default)
    {
        Require(caller, ManagePermission);

        var employee = await employees.GetAsync(caller.TenantKey, ParseId(request.EmployeeId), cancellationToken)
            ?? throw new MobileValidationException("EMPLOYEE_NOT_FOUND", "Choose an employee of this company.");
        if (!employee.IsActive)
        {
            throw new MobileValidationException("EMPLOYEE_INACTIVE", $"{employee.DisplayName} is no longer active.");
        }

        var type = LoanTypes.All.FirstOrDefault(item => Same(item, request.Type))
            ?? throw new MobileValidationException("LOAN_TYPE_UNKNOWN", "Choose Loan or Advance.");

        if (request.Principal <= 0 || request.Principal > 10_000_000m)
        {
            throw new MobileValidationException("AMOUNT_INVALID", "Amount must be between ₹1 and ₹1,00,00,000.");
        }

        if (request.Installments is < 1 or > 120)
        {
            throw new MobileValidationException("INSTALLMENTS_INVALID", "Instalments must be between 1 and 120 months.");
        }

        if (request.StartMonth is < 1 or > 12 || request.StartYear is < 2000 or > 2100)
        {
            throw new MobileValidationException("START_INVALID", "Choose the payroll month recovery starts from.");
        }

        // Only an advance carries a type (Festival, Medical, Travel…); a loan never does.
        string? advanceTypeCode = null;
        if (type == LoanTypes.Advance && !string.IsNullOrWhiteSpace(request.AdvanceTypeCode))
        {
            var advanceTypes = await masters.GetActiveAsync(caller.TenantKey, MasterCategories.AdvanceType, cancellationToken);
            advanceTypeCode = advanceTypes.FirstOrDefault(option => Same(option.Code, request.AdvanceTypeCode))?.Code
                ?? throw new MobileValidationException("ADVANCE_TYPE_UNKNOWN", "Choose a valid advance type.");
        }

        // An advance is never interest-bearing, whatever the form sent.
        var interest = type == LoanTypes.Advance ? 0m : request.InterestPercent;
        if (interest is < 0 or > 36)
        {
            throw new MobileValidationException("INTEREST_INVALID", "Interest must be between 0% and 36% a year.");
        }

        var principal = Math.Round(request.Principal, 0, MidpointRounding.AwayFromZero);
        var total = Math.Round(principal * (1 + interest / 100m * request.Installments / 12m), 0, MidpointRounding.AwayFromZero);
        var installment = Math.Ceiling(total / request.Installments);

        // Whole-rupee instalments rounded up can finish early; trim the count so none is zero.
        var installments = (int)Math.Ceiling(total / installment);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var now = DateTimeOffset.UtcNow;

        var created = await loans.InsertLoanAsync(new LoanRecord
        {
            TenantKey = caller.TenantKey,
            EmployeeId = employee.Id,
            Type = type,
            Principal = principal,
            InterestPercent = interest,
            TotalRepayable = total,
            Installments = installments,
            MonthlyInstallment = installment,
            StartYear = request.StartYear,
            StartMonth = request.StartMonth,
            Purpose = string.IsNullOrWhiteSpace(request.Purpose) ? null : request.Purpose.Trim(),
            AdvanceTypeCode = advanceTypeCode,
            RequestedOn = today,
            DisbursedOn = request.DisbursedOn,
            Status = LoanStatuses.Pending,
            RequestedBy = caller.UserId,
            CreatedUtc = now,
            UpdatedUtc = now
        }, cancellationToken);

        var context = await LoadAsync(caller.TenantKey, cancellationToken);
        return context.ToItem(created);
    }

    public async Task<LoanListItemDto> DecideAsync(
        MobileCallerContext caller, LoanDecisionRequest request, CancellationToken cancellationToken = default)
    {
        Require(caller, ManagePermission);

        var loan = await LoadLoanAsync(caller.TenantKey, request.LoanId, cancellationToken);
        if (loan.Status != LoanStatuses.Pending)
        {
            throw new MobileValidationException("LOAN_NOT_PENDING", $"{loan.VoucherNo} is already {loan.Status.ToLowerInvariant()}.");
        }

        var now = DateTimeOffset.UtcNow;
        loan.Status = request.Approve ? LoanStatuses.Active : LoanStatuses.Rejected;
        loan.DisbursedOn = request.Approve ? loan.DisbursedOn ?? DateOnly.FromDateTime(DateTime.Today) : null;
        loan.DecidedBy = caller.UserId;
        loan.DecidedUtc = now;
        loan.DecisionNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        loan.UpdatedUtc = now;

        await loans.UpdateLoanAsync(loan, cancellationToken);
        return (await LoadAsync(caller.TenantKey, cancellationToken)).ToItem(loan);
    }

    public async Task<LoanListItemDto> RecordRepaymentAsync(
        MobileCallerContext caller, RecordLoanRepaymentRequest request, CancellationToken cancellationToken = default)
    {
        Require(caller, ManagePermission);

        var loan = await LoadLoanAsync(caller.TenantKey, request.LoanId, cancellationToken);
        if (loan.Status != LoanStatuses.Active)
        {
            throw new MobileValidationException("LOAN_NOT_ACTIVE", "Repayments can only be recorded against an active loan.");
        }

        var outstanding = loan.TotalRepayable - (await loans.GetRepaymentsAsync(caller.TenantKey, loan.Id, cancellationToken)).Sum(item => item.Amount);
        var amount = Math.Round(request.Amount, 2);

        if (amount <= 0 || amount > outstanding)
        {
            throw new MobileValidationException(
                "REPAYMENT_INVALID", $"Enter an amount between ₹1 and the outstanding {Rupees(outstanding)}.");
        }

        await loans.AddRepaymentAsync(new LoanRepaymentRecord
        {
            TenantKey = caller.TenantKey,
            LoanId = loan.Id,
            Year = request.RecordedOn.Year,
            Month = request.RecordedOn.Month,
            Amount = amount,
            Source = LoanRepaymentSources.Manual,
            RecordedOn = request.RecordedOn,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            RecordedBy = caller.UserId,
            CreatedUtc = DateTimeOffset.UtcNow
        }, cancellationToken);

        await CloseIfSettledAsync(loan, cancellationToken);
        return (await LoadAsync(caller.TenantKey, cancellationToken)).ToItem(loan);
    }

    public async Task<LoanListItemDto> CancelAsync(
        MobileCallerContext caller, string loanId, string? note, CancellationToken cancellationToken = default)
    {
        Require(caller, ManagePermission);

        var loan = await LoadLoanAsync(caller.TenantKey, loanId, cancellationToken);
        var recovered = (await loans.GetRepaymentsAsync(caller.TenantKey, loan.Id, cancellationToken)).Sum(item => item.Amount);

        // Money already recovered is a record that must stay; such a loan settles, it is not cancelled.
        if (loan.Status is not (LoanStatuses.Pending or LoanStatuses.Active) || recovered > 0)
        {
            throw new MobileValidationException(
                "LOAN_NOT_CANCELLABLE",
                recovered > 0
                    ? $"{loan.VoucherNo} has {Rupees(recovered)} recovered already. Record the remaining repayment instead."
                    : $"{loan.VoucherNo} is {loan.Status.ToLowerInvariant()} and cannot be cancelled.");
        }

        var now = DateTimeOffset.UtcNow;
        loan.Status = LoanStatuses.Cancelled;
        loan.DecisionNote = string.IsNullOrWhiteSpace(note) ? loan.DecisionNote : note.Trim();
        loan.DecidedBy ??= caller.UserId;
        loan.DecidedUtc ??= now;
        loan.UpdatedUtc = now;

        await loans.UpdateLoanAsync(loan, cancellationToken);
        return (await LoadAsync(caller.TenantKey, cancellationToken)).ToItem(loan);
    }

    // ---- Payroll ------------------------------------------------------------

    public async Task<IReadOnlyDictionary<long, IReadOnlyList<LoanRecoveryDto>>> GetDueRecoveriesAsync(
        string tenantKey, int year, int month, CancellationToken cancellationToken = default)
    {
        var monthIndex = year * 12 + month;
        var active = (await loans.GetLoansAsync(tenantKey, cancellationToken))
            .Where(loan => loan.Status == LoanStatuses.Active && loan.StartYear * 12 + loan.StartMonth <= monthIndex)
            .ToList();

        if (active.Count == 0)
        {
            return new Dictionary<long, IReadOnlyList<LoanRecoveryDto>>();
        }

        var repayments = (await loans.GetRepaymentsAsync(tenantKey, null, cancellationToken))
            .GroupBy(item => item.LoanId)
            .ToDictionary(group => group.Key, group => group.ToList());

        return active
            .Select(loan =>
            {
                var paid = repayments.GetValueOrDefault(loan.Id) ?? [];

                // A month payroll has already recovered is not due again.
                if (paid.Any(item => item.Source == LoanRepaymentSources.Payroll && item.Year == year && item.Month == month))
                {
                    return null;
                }

                // Recoveries recorded for this same month are excluded, so regenerating the month
                // before approval sees the same outstanding balance each time.
                var outstanding = loan.TotalRepayable - paid.Sum(item => item.Amount);
                var amount = Math.Min(loan.MonthlyInstallment, outstanding);

                return amount > 0 ? new LoanRecoveryDto(loan.Id, loan.VoucherNo, loan.Type, amount) : null;
            })
            .OfType<LoanRecoveryDto>()
            .GroupBy(recovery => active.First(loan => loan.Id == recovery.LoanId).EmployeeId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<LoanRecoveryDto>)group.ToList());
    }

    public async Task<int> RecordPayrollRecoveriesAsync(
        string tenantKey, long payrollRunId, int year, int month,
        IReadOnlyList<(long LoanId, decimal Amount)> recoveries, string? recordedBy,
        CancellationToken cancellationToken = default)
    {
        var recorded = 0;
        var today = DateOnly.FromDateTime(DateTime.Today);

        foreach (var (loanId, amount) in recoveries.Where(item => item.Amount > 0))
        {
            var loan = await loans.GetLoanAsync(tenantKey, loanId, cancellationToken);
            if (loan is null)
            {
                continue;
            }

            var added = await loans.AddRepaymentAsync(new LoanRepaymentRecord
            {
                TenantKey = tenantKey,
                LoanId = loanId,
                Year = year,
                Month = month,
                Amount = amount,
                Source = LoanRepaymentSources.Payroll,
                PayrollRunId = payrollRunId,
                RecordedOn = today,
                Note = $"Payroll {MonthLabel(year, month)}",
                RecordedBy = recordedBy,
                CreatedUtc = DateTimeOffset.UtcNow
            }, cancellationToken);

            if (added)
            {
                recorded++;
                await CloseIfSettledAsync(loan, cancellationToken);
            }
        }

        return recorded;
    }

    // ---- Shared -------------------------------------------------------------

    private sealed class LoanContext(
        IReadOnlyList<LoanRecord> loans,
        IReadOnlyList<LoanRepaymentRecord> repayments,
        IReadOnlyDictionary<long, EmployeeRecord> people,
        IReadOnlyDictionary<string, string> divisionNames,
        IReadOnlyDictionary<string, string> departmentNames,
        IReadOnlyDictionary<string, string> advanceTypeNames)
    {
        public IReadOnlyList<LoanRecord> Loans { get; } = loans;
        public IReadOnlyList<LoanRepaymentRecord> Repayments { get; } = repayments;

        public IReadOnlyList<LoanRepaymentRecord> RepaymentsOf(long loanId) =>
            Repayments.Where(item => item.LoanId == loanId).ToList();

        public string? DivisionOf(LoanListItemDto item) =>
            people.GetValueOrDefault(long.Parse(item.EmployeeId, CultureInfo.InvariantCulture))?.DivisionCode;

        public string? DepartmentOf(LoanListItemDto item) =>
            people.GetValueOrDefault(long.Parse(item.EmployeeId, CultureInfo.InvariantCulture))?.DepartmentCode;

        public EmployeeRecord? PersonOf(LoanListItemDto item) =>
            people.GetValueOrDefault(long.Parse(item.EmployeeId, CultureInfo.InvariantCulture));

        public LoanListItemDto ToItem(LoanRecord loan)
        {
            var person = people.GetValueOrDefault(loan.EmployeeId);
            var recovered = RepaymentsOf(loan.Id).Sum(item => item.Amount);
            var outstanding = loan.Status is LoanStatuses.Active or LoanStatuses.Closed
                ? Math.Max(0, loan.TotalRepayable - recovered)
                : 0;

            return new LoanListItemDto(
                loan.Id.ToString(CultureInfo.InvariantCulture),
                loan.VoucherNo,
                loan.EmployeeId.ToString(CultureInfo.InvariantCulture),
                person?.EmployeeNumber ?? "—",
                person?.DisplayName ?? "Unknown employee",
                NameOr(divisionNames, person?.DivisionCode, "Unassigned"),
                NameOr(departmentNames, person?.DepartmentCode, "Unassigned"),
                loan.Type,
                loan.Principal,
                loan.InterestPercent,
                loan.TotalRepayable,
                loan.Installments,
                loan.MonthlyInstallment,
                loan.StartYear,
                loan.StartMonth,
                MonthLabel(loan.StartYear, loan.StartMonth),
                recovered,
                outstanding,
                loan.MonthlyInstallment <= 0 ? 0 : Math.Min(loan.Installments, (int)Math.Floor(recovered / loan.MonthlyInstallment)),
                loan.Status,
                loan.Purpose,
                loan.AdvanceTypeCode,
                loan.AdvanceTypeCode is null ? null : NameOr(advanceTypeNames, loan.AdvanceTypeCode, loan.AdvanceTypeCode),
                person?.JobTitle,
                loan.RequestedOn,
                loan.DisbursedOn,
                loan.DecidedUtc,
                loan.DecisionNote);
        }
    }

    private async Task<LoanContext> LoadAsync(string tenantKey, CancellationToken cancellationToken)
    {
        var allLoans = await loans.GetLoansAsync(tenantKey, cancellationToken);
        var repayments = await loans.GetRepaymentsAsync(tenantKey, null, cancellationToken);
        var people = (await employees.QueryAsync(tenantKey, new EmployeeQueryFilter(null, null, null, null, null), cancellationToken))
            .ToDictionary(person => person.Id);
        var options = await masters.GetAllAsync(tenantKey, cancellationToken);

        return new LoanContext(
            allLoans, repayments, people,
            NamesFor(options, MasterCategories.Division),
            NamesFor(options, MasterCategories.Department),
            NamesFor(options, MasterCategories.AdvanceType));
    }

    private async Task<LoanRecord> LoadLoanAsync(string tenantKey, string loanId, CancellationToken cancellationToken) =>
        await loans.GetLoanAsync(tenantKey, ParseId(loanId), cancellationToken)
        ?? throw new MobileNotFoundException("That loan no longer exists.");

    private async Task CloseIfSettledAsync(LoanRecord loan, CancellationToken cancellationToken)
    {
        var recovered = (await loans.GetRepaymentsAsync(loan.TenantKey, loan.Id, cancellationToken)).Sum(item => item.Amount);
        if (loan.Status == LoanStatuses.Active && recovered >= loan.TotalRepayable)
        {
            loan.Status = LoanStatuses.Closed;
            loan.UpdatedUtc = DateTimeOffset.UtcNow;
            await loans.UpdateLoanAsync(loan, cancellationToken);
        }
    }

    private static (int Year, int Month) AddMonths(int year, int month, int offset)
    {
        var index = year * 12 + (month - 1) + offset;
        return (index / 12, index % 12 + 1);
    }

    private static string MonthLabel(int year, int month) =>
        new DateTime(year, month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture);

    private static string Rupees(decimal amount) =>
        string.Format(CultureInfo.GetCultureInfo("en-IN"), "₹{0:N0}", amount);

    private static Dictionary<string, string> NamesFor(IReadOnlyList<MasterOptionRecord> options, string category) =>
        options
            .Where(option => option.Category == category)
            .GroupBy(option => option.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

    private static string NameOr(IReadOnlyDictionary<string, string> names, string? code, string fallback) =>
        string.IsNullOrWhiteSpace(code) ? fallback : names.GetValueOrDefault(code) ?? code;

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static bool Same(string? left, string? right) =>
        string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);

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
