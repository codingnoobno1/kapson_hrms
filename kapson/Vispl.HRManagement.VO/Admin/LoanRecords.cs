namespace Vispl.HRManagement.VO.Admin;

public sealed class LoanRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public string VoucherNo { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public required string Type { get; set; }
    public decimal Principal { get; set; }
    public decimal InterestPercent { get; set; }
    public decimal TotalRepayable { get; set; }
    public int Installments { get; set; }
    public decimal MonthlyInstallment { get; set; }
    public int StartYear { get; set; }
    public int StartMonth { get; set; }
    public string? Purpose { get; set; }

    /// <summary>Advance only: the Advance Type master code (Festival, Medical, Travel…). Null for a loan.</summary>
    public string? AdvanceTypeCode { get; set; }

    public DateOnly RequestedOn { get; set; }
    public DateOnly? DisbursedOn { get; set; }
    public required string Status { get; set; }
    public string? RequestedBy { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }
    public string? DecisionNote { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

public sealed class LoanRepaymentRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public long LoanId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public required string Source { get; set; }
    public long? PayrollRunId { get; set; }
    public DateOnly RecordedOn { get; set; }
    public string? Note { get; set; }
    public string? RecordedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
