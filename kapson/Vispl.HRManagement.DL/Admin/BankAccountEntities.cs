namespace Vispl.HRManagement.DL.Admin;

public sealed class CompanyBankAccountEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string AccountLabel { get; set; } = string.Empty;
    public string AccountNumberProtected { get; set; } = string.Empty;
    public string AccountLast4 { get; set; } = string.Empty;
    public string Ifsc { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class EmployeeBankAccountEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string AccountNumberProtected { get; set; } = string.Empty;
    public string AccountLast4 { get; set; } = string.Empty;
    public string Ifsc { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
