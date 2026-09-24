using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class BankAccountRepository(HrManagementDbContext dbContext) : IBankAccountRepository
{
    public async Task<IReadOnlyList<CompanyBankAccountRecord>> GetCompanyAccountsAsync(
        string tenantKey, CancellationToken cancellationToken = default) =>
        (await dbContext.CompanyBankAccounts.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey)
            .OrderByDescending(item => item.IsDefault).ThenBy(item => item.AccountLabel)
            .ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<long> UpsertCompanyAccountAsync(
        CompanyBankAccountRecord account, CancellationToken cancellationToken = default)
    {
        var entity = account.Id == 0
            ? null
            : await dbContext.CompanyBankAccounts.SingleOrDefaultAsync(
                item => item.Id == account.Id && item.TenantKey == account.TenantKey, cancellationToken);

        if (entity is null)
        {
            entity = new CompanyBankAccountEntity();
            dbContext.CompanyBankAccounts.Add(entity);
        }

        entity.TenantKey = account.TenantKey;
        entity.BankName = account.BankName;
        entity.AccountLabel = account.AccountLabel;
        entity.AccountNumberProtected = account.AccountNumberProtected;
        entity.AccountLast4 = account.AccountLast4;
        entity.Ifsc = account.Ifsc;
        entity.BranchName = account.BranchName;
        entity.IsDefault = account.IsDefault;
        entity.IsActive = account.IsActive;
        entity.UpdatedUtc = account.UpdatedUtc;

        if (account.IsDefault)
        {
            // One default per tenant, cleared in the same save so there is never a moment with two.
            var others = await dbContext.CompanyBankAccounts
                .Where(item => item.TenantKey == account.TenantKey && item.IsDefault && item.Id != account.Id)
                .ToListAsync(cancellationToken);

            foreach (var other in others)
            {
                other.IsDefault = false;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<bool> DeleteCompanyAccountAsync(
        string tenantKey, long accountId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.CompanyBankAccounts.SingleOrDefaultAsync(
            item => item.TenantKey == tenantKey && item.Id == accountId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        dbContext.CompanyBankAccounts.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<EmployeeBankAccountRecord>> GetEmployeeAccountsAsync(
        string tenantKey, CancellationToken cancellationToken = default) =>
        (await dbContext.EmployeeBankAccounts.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey)
            .ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<EmployeeBankAccountRecord?> GetEmployeeAccountAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.EmployeeBankAccounts.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpsertEmployeeAccountAsync(
        EmployeeBankAccountRecord account, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.EmployeeBankAccounts.SingleOrDefaultAsync(
            item => item.TenantKey == account.TenantKey && item.EmployeeId == account.EmployeeId, cancellationToken);

        if (entity is null)
        {
            entity = new EmployeeBankAccountEntity { TenantKey = account.TenantKey, EmployeeId = account.EmployeeId };
            dbContext.EmployeeBankAccounts.Add(entity);
        }

        entity.BankName = account.BankName;
        entity.AccountHolderName = account.AccountHolderName;
        entity.AccountNumberProtected = account.AccountNumberProtected;
        entity.AccountLast4 = account.AccountLast4;
        entity.Ifsc = account.Ifsc;
        entity.AccountType = account.AccountType;
        entity.UpdatedBy = account.UpdatedBy;
        entity.UpdatedUtc = account.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> RemoveEmployeeAccountAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.EmployeeBankAccounts.SingleOrDefaultAsync(
            item => item.TenantKey == tenantKey && item.EmployeeId == employeeId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        dbContext.EmployeeBankAccounts.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static CompanyBankAccountRecord ToRecord(CompanyBankAccountEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        BankName = entity.BankName,
        AccountLabel = entity.AccountLabel,
        AccountNumberProtected = entity.AccountNumberProtected,
        AccountLast4 = entity.AccountLast4,
        Ifsc = entity.Ifsc,
        BranchName = entity.BranchName,
        IsDefault = entity.IsDefault,
        IsActive = entity.IsActive,
        UpdatedUtc = entity.UpdatedUtc
    };

    private static EmployeeBankAccountRecord ToRecord(EmployeeBankAccountEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        EmployeeId = entity.EmployeeId,
        BankName = entity.BankName,
        AccountHolderName = entity.AccountHolderName,
        AccountNumberProtected = entity.AccountNumberProtected,
        AccountLast4 = entity.AccountLast4,
        Ifsc = entity.Ifsc,
        AccountType = entity.AccountType,
        UpdatedBy = entity.UpdatedBy,
        UpdatedUtc = entity.UpdatedUtc
    };
}
