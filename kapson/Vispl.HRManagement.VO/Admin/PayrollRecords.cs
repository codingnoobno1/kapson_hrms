namespace Vispl.HRManagement.VO.Admin;

/// <summary>One month's payroll for a tenant and salary type.</summary>
public sealed class PayrollRunRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public required string SalaryType { get; set; }
    public DateTimeOffset GeneratedUtc { get; set; }
    public string? GeneratedBy { get; set; }
    public DateTimeOffset? ApprovedUtc { get; set; }
    public string? ApprovedBy { get; set; }
}

/// <summary>
/// One employee's generated salary. Name, division, department and designation are copied at
/// generation time, so a later transfer or rename does not rewrite a month already paid.
/// </summary>
public sealed class PayrollLineRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public long RunId { get; set; }
    public long EmployeeId { get; set; }
    public required string EmployeeNumber { get; set; }
    public required string DisplayName { get; set; }
    public required string DivisionCode { get; set; }
    public required string DepartmentCode { get; set; }
    public required string Designation { get; set; }
    public required string GradeCode { get; set; }
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
    public required string BreakdownJson { get; set; }
    public required string Status { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? PaymentMode { get; set; }
    public string? DocumentType { get; set; }
    public long? TransferBatchId { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

public sealed class BankTransferBatchRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public long RunId { get; set; }
    public required string ReferenceNo { get; set; }
    public long CompanyBankAccountId { get; set; }
    public DateOnly TransferDate { get; set; }
    public required string TransferMode { get; set; }
    public decimal TotalAmount { get; set; }
    public int EmployeeCount { get; set; }
    public string? CreatedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
