namespace Vispl.HRManagement.DL.Admin;

public sealed class PayrollRunEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public string SalaryType { get; set; } = string.Empty;
    public DateTimeOffset GeneratedUtc { get; set; }
    public string? GeneratedBy { get; set; }
    public DateTimeOffset? ApprovedUtc { get; set; }
    public string? ApprovedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class PayrollLineEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long RunId { get; set; }
    public long EmployeeId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string DivisionCode { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string GradeCode { get; set; } = string.Empty;
    public string? ShiftCode { get; set; }
    public int WorkedDays { get; set; }
    public int ExpectedDays { get; set; }
    public int PayableDays { get; set; }
    public int DaysInMonth { get; set; }
    public decimal Basic { get; set; }
    public decimal Hra { get; set; }
    public decimal Conveyance { get; set; }
    public decimal OtherAllowances { get; set; }
    public decimal Gross { get; set; }
    public decimal Pf { get; set; }
    public decimal Esi { get; set; }
    public decimal Tds { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal Deductions { get; set; }
    public decimal NetPayable { get; set; }
    public string BreakdownJson { get; set; } = "{}";
    public string Status { get; set; } = string.Empty;
    public DateOnly? PaidOn { get; set; }
    public string? PaymentMode { get; set; }
    public string? DocumentType { get; set; }
    public long? TransferBatchId { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class BankTransferBatchEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long RunId { get; set; }
    public string ReferenceNo { get; set; } = string.Empty;
    public long CompanyBankAccountId { get; set; }
    public DateOnly TransferDate { get; set; }
    public string TransferMode { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int EmployeeCount { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
