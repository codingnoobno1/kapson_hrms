using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Employee;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Payroll;

public sealed record PayslipListItemDto(
    string Id,
    string Period,
    DateTimeOffset PublishedUtc,
    MoneyDto? NetPay,
    bool IsProtectedDownloadAvailable);

public sealed record PayslipDetailDto(
    string Id,
    string Period,
    IReadOnlyList<PayComponentDto> Earnings,
    IReadOnlyList<PayComponentDto> Deductions,
    MoneyDto NetPay,
    MoneyDto YearToDateGross,
    MoneyDto YearToDateTax,
    string? RowVersion);

public sealed record TaxSummaryDto(
    int FinancialYear,
    string Regime,
    MoneyDto TaxableIncome,
    MoneyDto TaxPaid,
    MoneyDto TaxProjected,
    IReadOnlyList<PayComponentDto> Deductions);

public sealed record SalaryStructureDto(
    DateOnly EffectiveFrom,
    MoneyDto AnnualTotal,
    IReadOnlyList<PayComponentDto> Components);

public sealed record ReimbursementListItemDto(
    string Id,
    string TypeCode,
    DateOnly ExpenseDate,
    MoneyDto Amount,
    StatusDto Status,
    DateTimeOffset? SubmittedUtc,
    DateTimeOffset? PaidUtc);

public sealed record ReimbursementDetailDto(
    ReimbursementListItemDto Summary,
    string Merchant,
    string Description,
    IReadOnlyList<AttachmentMetadataDto> Receipts,
    IReadOnlyList<WorkflowStepDto> Workflow,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);

public sealed class SaveReimbursementRequest
{
    [Required, StringLength(80)] public string TypeCode { get; set; } = string.Empty;
    public DateOnly ExpenseDate { get; set; }
    [Range(0.01, 999999999)] public decimal Amount { get; set; }
    [Required, StringLength(3, MinimumLength = 3)] public string CurrencyCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string Merchant { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Description { get; set; } = string.Empty;
    public IReadOnlyList<string> ReceiptIds { get; set; } = [];
    [StringLength(200)] public string? RowVersion { get; set; }
}

public sealed record BenefitPlanDto(
    string Id,
    string Category,
    string Name,
    string Provider,
    string CoverageSummary,
    MoneyDto EmployeeCost,
    string Eligibility);

public sealed record BenefitCoverageDto(
    BenefitPlanDto Plan,
    IReadOnlyList<PersonReferenceDto> CoveredDependants,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);
