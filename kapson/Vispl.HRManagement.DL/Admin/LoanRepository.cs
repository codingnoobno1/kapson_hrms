using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class LoanRepository(HrManagementDbContext dbContext) : ILoanRepository
{
    public async Task<IReadOnlyList<LoanRecord>> GetLoansAsync(string tenantKey, CancellationToken cancellationToken = default) =>
        (await dbContext.Loans.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey)
            .OrderByDescending(item => item.CreatedUtc)
            .ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<LoanRecord?> GetLoanAsync(string tenantKey, long loanId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Loans.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == loanId, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<LoanRecord> InsertLoanAsync(LoanRecord loan, CancellationToken cancellationToken = default)
    {
        var prefix = $"LA-{loan.RequestedOn.Year}-";

        // The voucher number is unique per tenant; a concurrent insert taking the same number
        // fails the unique index and is retried with the next one.
        for (var attempt = 0; ; attempt++)
        {
            var last = await dbContext.Loans.AsNoTracking()
                .Where(item => item.TenantKey == loan.TenantKey && item.VoucherNo.StartsWith(prefix))
                .OrderByDescending(item => item.VoucherNo)
                .Select(item => item.VoucherNo)
                .FirstOrDefaultAsync(cancellationToken);

            var next = last is not null && int.TryParse(last[prefix.Length..], out var number) ? number + 1 : 1;

            var entity = new LoanEntity();
            Copy(loan, entity);
            entity.VoucherNo = $"{prefix}{next:0000}";
            dbContext.Loans.Add(entity);

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return ToRecord(entity);
            }
            catch (DbUpdateException) when (attempt < 3)
            {
                dbContext.Entry(entity).State = EntityState.Detached;
            }
        }
    }

    public async Task UpdateLoanAsync(LoanRecord loan, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Loans.SingleAsync(
            item => item.TenantKey == loan.TenantKey && item.Id == loan.Id, cancellationToken);

        var voucher = entity.VoucherNo;
        Copy(loan, entity);
        entity.VoucherNo = voucher;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoanRepaymentRecord>> GetRepaymentsAsync(
        string tenantKey, long? loanId, CancellationToken cancellationToken = default) =>
        (await dbContext.LoanRepayments.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && (loanId == null || item.LoanId == loanId))
            .OrderBy(item => item.Year).ThenBy(item => item.Month).ThenBy(item => item.Id)
            .ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<bool> AddRepaymentAsync(LoanRepaymentRecord repayment, CancellationToken cancellationToken = default)
    {
        if (repayment.Source == "Payroll" &&
            await dbContext.LoanRepayments.AnyAsync(item =>
                item.TenantKey == repayment.TenantKey && item.LoanId == repayment.LoanId &&
                item.Year == repayment.Year && item.Month == repayment.Month && item.Source == "Payroll",
                cancellationToken))
        {
            return false;
        }

        var entity = new LoanRepaymentEntity
        {
            TenantKey = repayment.TenantKey,
            LoanId = repayment.LoanId,
            Year = repayment.Year,
            Month = repayment.Month,
            Amount = repayment.Amount,
            Source = repayment.Source,
            PayrollRunId = repayment.PayrollRunId,
            RecordedOn = repayment.RecordedOn,
            Note = repayment.Note,
            RecordedBy = repayment.RecordedBy,
            CreatedUtc = repayment.CreatedUtc
        };
        dbContext.LoanRepayments.Add(entity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException) when (repayment.Source == "Payroll")
        {
            // The filtered unique index caught a concurrent duplicate payroll recovery.
            dbContext.Entry(entity).State = EntityState.Detached;
            return false;
        }
    }

    private static void Copy(LoanRecord loan, LoanEntity entity)
    {
        entity.TenantKey = loan.TenantKey;
        entity.VoucherNo = loan.VoucherNo;
        entity.EmployeeId = loan.EmployeeId;
        entity.Type = loan.Type;
        entity.Principal = loan.Principal;
        entity.InterestPercent = loan.InterestPercent;
        entity.TotalRepayable = loan.TotalRepayable;
        entity.Installments = loan.Installments;
        entity.MonthlyInstallment = loan.MonthlyInstallment;
        entity.StartYear = loan.StartYear;
        entity.StartMonth = loan.StartMonth;
        entity.Purpose = loan.Purpose;
        entity.AdvanceTypeCode = loan.AdvanceTypeCode;
        entity.RequestedOn = loan.RequestedOn;
        entity.DisbursedOn = loan.DisbursedOn;
        entity.Status = loan.Status;
        entity.RequestedBy = loan.RequestedBy;
        entity.DecidedBy = loan.DecidedBy;
        entity.DecidedUtc = loan.DecidedUtc;
        entity.DecisionNote = loan.DecisionNote;
        entity.CreatedUtc = loan.CreatedUtc;
        entity.UpdatedUtc = loan.UpdatedUtc;
    }

    private static LoanRecord ToRecord(LoanEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        VoucherNo = entity.VoucherNo,
        EmployeeId = entity.EmployeeId,
        Type = entity.Type,
        Principal = entity.Principal,
        InterestPercent = entity.InterestPercent,
        TotalRepayable = entity.TotalRepayable,
        Installments = entity.Installments,
        MonthlyInstallment = entity.MonthlyInstallment,
        StartYear = entity.StartYear,
        StartMonth = entity.StartMonth,
        Purpose = entity.Purpose,
        AdvanceTypeCode = entity.AdvanceTypeCode,
        RequestedOn = entity.RequestedOn,
        DisbursedOn = entity.DisbursedOn,
        Status = entity.Status,
        RequestedBy = entity.RequestedBy,
        DecidedBy = entity.DecidedBy,
        DecidedUtc = entity.DecidedUtc,
        DecisionNote = entity.DecisionNote,
        CreatedUtc = entity.CreatedUtc,
        UpdatedUtc = entity.UpdatedUtc
    };

    private static LoanRepaymentRecord ToRecord(LoanRepaymentEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        LoanId = entity.LoanId,
        Year = entity.Year,
        Month = entity.Month,
        Amount = entity.Amount,
        Source = entity.Source,
        PayrollRunId = entity.PayrollRunId,
        RecordedOn = entity.RecordedOn,
        Note = entity.Note,
        RecordedBy = entity.RecordedBy,
        CreatedUtc = entity.CreatedUtc
    };
}
