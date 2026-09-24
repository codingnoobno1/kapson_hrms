namespace Vispl.HRManagement.VO.Admin;

public sealed class CompanyBankAccountRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public required string BankName { get; set; }
    public required string AccountLabel { get; set; }

    /// <summary>Encrypted; never the plain number.</summary>
    public required string AccountNumberProtected { get; set; }

    public required string AccountLast4 { get; set; }
    public required string Ifsc { get; set; }
    public string? BranchName { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

public sealed class EmployeeBankAccountRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public long EmployeeId { get; set; }
    public required string BankName { get; set; }
    public required string AccountHolderName { get; set; }

    /// <summary>Encrypted; never the plain number.</summary>
    public required string AccountNumberProtected { get; set; }

    public required string AccountLast4 { get; set; }
    public required string Ifsc { get; set; }
    public required string AccountType { get; set; }

    /// <summary>The account that last changed this record, for audit.</summary>
    public string? UpdatedBy { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }
}
