namespace Vispl.HRManagement.DL.Admin;

public sealed class LoanEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string VoucherNo { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal Principal { get; set; }
    public decimal InterestPercent { get; set; }
    public decimal TotalRepayable { get; set; }
    public int Installments { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public string? Purpose { get; set; }
    public string? AdvanceTypeCode { get; set; }
    public DateOnly RequestedOn { get; set; }
    public DateOnly? DisbursedOn { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RequestedBy { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }
    public string? DecisionNote { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class LoanRepaymentEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long LoanId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public string Source { get; set; } = string.Empty;
    public long? PayrollRunId { get; set; }
    public DateOnly RecordedOn { get; set; }
    public string? Note { get; set; }
    public string? RecordedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
