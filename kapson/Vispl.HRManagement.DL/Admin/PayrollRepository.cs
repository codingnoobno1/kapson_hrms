using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class PayrollRepository(HrManagementDbContext dbContext) : IPayrollRepository
{
    public async Task<PayrollRunRecord?> GetRunAsync(
        string tenantKey, int year, int month, string salaryType, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PayrollRuns.AsNoTracking().SingleOrDefaultAsync(
            item => item.TenantKey == tenantKey && item.Year == year && item.Month == month && item.SalaryType == salaryType,
            cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<PayrollRunRecord> GetOrCreateRunAsync(PayrollRunRecord run, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PayrollRuns.SingleOrDefaultAsync(
            item => item.TenantKey == run.TenantKey && item.Year == run.Year && item.Month == run.Month && item.SalaryType == run.SalaryType,
            cancellationToken);

        if (entity is null)
        {
            entity = new PayrollRunEntity
            {
                TenantKey = run.TenantKey,
                Year = run.Year,
                Month = run.Month,
                SalaryType = run.SalaryType
            };
            dbContext.PayrollRuns.Add(entity);
        }

        entity.GeneratedUtc = run.GeneratedUtc;
        entity.GeneratedBy = run.GeneratedBy;

        await dbContext.SaveChangesAsync(cancellationToken);
        return ToRecord(entity);
    }

    public async Task ApproveRunAsync(
        string tenantKey, long runId, string? approvedBy, DateTimeOffset approvedUtc, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PayrollRuns.SingleAsync(
            item => item.TenantKey == tenantKey && item.Id == runId, cancellationToken);

        entity.ApprovedUtc = approvedUtc;
        entity.ApprovedBy = approvedBy;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollLineRecord>> GetLinesAsync(
        string tenantKey, long runId, CancellationToken cancellationToken = default) =>
        (await dbContext.PayrollLines.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.RunId == runId)
            .ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task UpsertLinesAsync(IReadOnlyList<PayrollLineRecord> lines, CancellationToken cancellationToken = default)
    {
        if (lines.Count == 0)
        {
            return;
        }

        var tenantKey = lines[0].TenantKey;
        var runId = lines[0].RunId;
        var employeeIds = lines.Select(line => line.EmployeeId).ToList();

        var existing = await dbContext.PayrollLines
            .Where(item => item.TenantKey == tenantKey && item.RunId == runId && employeeIds.Contains(item.EmployeeId))
            .ToDictionaryAsync(item => item.EmployeeId, cancellationToken);

        foreach (var line in lines)
        {
            if (!existing.TryGetValue(line.EmployeeId, out var entity))
            {
                entity = new PayrollLineEntity { TenantKey = line.TenantKey, RunId = line.RunId, EmployeeId = line.EmployeeId };
                dbContext.PayrollLines.Add(entity);
            }

            Copy(line, entity);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PayrollLineRecord>> RecordPaymentAsync(
        string tenantKey,
        long runId,
        IReadOnlyCollection<long> employeeIds,
        string newStatus,
        DateOnly paidOn,
        string paymentMode,
        string documentType,
        BankTransferBatchRecord? batch,
        string? updatedBy,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        // The context uses SQL Server's retrying execution strategy, which refuses a transaction
        // opened outside it. The whole unit — read, batch, status change — runs inside the
        // strategy so a transient failure retries all of it, and it re-reads the lines each
        // attempt so a retry never works from stale state.
        var strategy = dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();

            // Only lines still Generated move: a line already paid or transferred is never paid twice.
            var entities = await dbContext.PayrollLines
                .Where(item => item.TenantKey == tenantKey && item.RunId == runId &&
                               employeeIds.Contains(item.EmployeeId) && item.Status == "Generated")
                .ToListAsync(cancellationToken);

            if (entities.Count == 0)
            {
                return (IReadOnlyList<PayrollLineRecord>)[];
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            long? batchId = null;
            if (batch is not null)
            {
                var batchEntity = new BankTransferBatchEntity
                {
                    TenantKey = batch.TenantKey,
                    RunId = batch.RunId,
                    ReferenceNo = batch.ReferenceNo,
                    CompanyBankAccountId = batch.CompanyBankAccountId,
                    TransferDate = batch.TransferDate,
                    TransferMode = batch.TransferMode,
                    TotalAmount = entities.Sum(item => item.NetPayable),
                    EmployeeCount = entities.Count,
                    CreatedBy = batch.CreatedBy,
                    CreatedUtc = batch.CreatedUtc
                };
                dbContext.BankTransferBatches.Add(batchEntity);
                await dbContext.SaveChangesAsync(cancellationToken);
                batchId = batchEntity.Id;
            }

            foreach (var entity in entities)
            {
                entity.Status = newStatus;
                entity.PaidOn = paidOn;
                entity.PaymentMode = paymentMode;
                entity.DocumentType = documentType;
                entity.TransferBatchId = batchId ?? entity.TransferBatchId;
                entity.UpdatedBy = updatedBy;
                entity.UpdatedUtc = now;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return (IReadOnlyList<PayrollLineRecord>)entities.Select(ToRecord).ToList();
        });
    }

    private static void Copy(PayrollLineRecord line, PayrollLineEntity entity)
    {
        entity.EmployeeNumber = line.EmployeeNumber;
        entity.DisplayName = line.DisplayName;
        entity.DivisionCode = line.DivisionCode;
        entity.DepartmentCode = line.DepartmentCode;
        entity.Designation = line.Designation;
        entity.GradeCode = line.GradeCode;
        entity.ShiftCode = line.ShiftCode;
        entity.WorkedDays = line.WorkedDays;
        entity.ExpectedDays = line.ExpectedDays;
        entity.PayableDays = line.PayableDays;
        entity.DaysInMonth = line.DaysInMonth;
        entity.Basic = line.Basic;
        entity.Hra = line.Hra;
        entity.Conveyance = line.Conveyance;
        entity.OtherAllowances = line.OtherAllowances;
        entity.Gross = line.Gross;
        entity.Pf = line.Pf;
        entity.Esi = line.Esi;
        entity.Tds = line.Tds;
        entity.OtherDeductions = line.OtherDeductions;
        entity.Deductions = line.Deductions;
        entity.NetPayable = line.NetPayable;
        entity.BreakdownJson = line.BreakdownJson;
        entity.Status = line.Status;
        entity.PaidOn = line.PaidOn;
        entity.PaymentMode = line.PaymentMode;
        entity.DocumentType = line.DocumentType;
        entity.TransferBatchId = line.TransferBatchId;
        entity.UpdatedBy = line.UpdatedBy;
        entity.UpdatedUtc = line.UpdatedUtc;
    }

    private static PayrollRunRecord ToRecord(PayrollRunEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        Year = entity.Year,
        Month = entity.Month,
        SalaryType = entity.SalaryType,
        GeneratedUtc = entity.GeneratedUtc,
        GeneratedBy = entity.GeneratedBy,
        ApprovedUtc = entity.ApprovedUtc,
        ApprovedBy = entity.ApprovedBy
    };

    private static PayrollLineRecord ToRecord(PayrollLineEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        RunId = entity.RunId,
        EmployeeId = entity.EmployeeId,
        EmployeeNumber = entity.EmployeeNumber,
        DisplayName = entity.DisplayName,
        DivisionCode = entity.DivisionCode,
        DepartmentCode = entity.DepartmentCode,
        Designation = entity.Designation,
        GradeCode = entity.GradeCode,
        ShiftCode = entity.ShiftCode,
        WorkedDays = entity.WorkedDays,
        ExpectedDays = entity.ExpectedDays,
        PayableDays = entity.PayableDays,
        DaysInMonth = entity.DaysInMonth,
        Basic = entity.Basic,
        Hra = entity.Hra,
        Conveyance = entity.Conveyance,
        OtherAllowances = entity.OtherAllowances,
        Gross = entity.Gross,
        Pf = entity.Pf,
        Esi = entity.Esi,
        Tds = entity.Tds,
        OtherDeductions = entity.OtherDeductions,
        Deductions = entity.Deductions,
        NetPayable = entity.NetPayable,
        BreakdownJson = entity.BreakdownJson,
        Status = entity.Status,
        PaidOn = entity.PaidOn,
        PaymentMode = entity.PaymentMode,
        DocumentType = entity.DocumentType,
        TransferBatchId = entity.TransferBatchId,
        UpdatedBy = entity.UpdatedBy,
        UpdatedUtc = entity.UpdatedUtc
    };
}
