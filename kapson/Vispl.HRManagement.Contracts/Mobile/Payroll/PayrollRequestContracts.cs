using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Payroll;

// Payslips and tax -----------------------------------------------------------

public sealed class PayslipListQuery : MobileListQuery
{
    public int? Year { get; set; }
    [StringLength(64)] public string? EmployeeId { get; set; }
}

public sealed class TaxSummaryQuery
{
    public int? FinancialYear { get; set; }
    [StringLength(64)] public string? EmployeeId { get; set; }
}

public sealed record TaxDeclarationLineDto(
    string Code,
    string SectionCode,
    string Name,
    decimal DeclaredAmount,
    decimal? VerifiedAmount,
    StatusDto Status,
    IReadOnlyList<AttachmentMetadataDto> Proofs,
    string? VerifierComment);

public sealed record TaxDeclarationDto(
    int FinancialYear,
    string RegimeCode,
    DateRangeDto Window,
    bool IsWindowOpen,
    IReadOnlyList<TaxDeclarationLineDto> Lines,
    decimal DeclaredTotal,
    decimal? VerifiedTotal,
    StatusDto Status,
    string? RowVersion);

public sealed class SaveTaxDeclarationRequest
{
    [Required] public int FinancialYear { get; set; }
    public IReadOnlyList<TaxDeclarationLineRequest> Lines { get; set; } = [];
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class TaxDeclarationLineRequest
{
    [Required, StringLength(40)] public string Code { get; set; } = string.Empty;
    public decimal DeclaredAmount { get; set; }
    public IReadOnlyList<string> ProofUploadIds { get; set; } = [];
}

public sealed class SubmitTaxDeclarationRequest
{
    [Required] public int FinancialYear { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class VerifyTaxDeclarationLineRequest
{
    [Required, StringLength(40)] public string LineCode { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    public decimal? VerifiedAmount { get; set; }
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

// Reimbursements (WF-006) -----------------------------------------------------

public sealed class ReimbursementListQuery : MobileListQuery
{
    [StringLength(40)] public string? State { get; set; }
    [StringLength(40)] public string? TypeCode { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed class SubmitReimbursementRequest
{
    [StringLength(2000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class ReimbursementDecisionRequest
{
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    public decimal? ApprovedAmount { get; set; }
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class ReimbursementReceiptRequest
{
    [Required, StringLength(260)] public string FileName { get; set; } = string.Empty;
    [Required, StringLength(120)] public string ContentType { get; set; } = string.Empty;
    [Range(1, long.MaxValue)] public long SizeBytes { get; set; }
}

// Benefits (WF-008) ------------------------------------------------------------

public sealed class BenefitPlansQuery : MobileListQuery
{
    [StringLength(40)] public string? CategoryCode { get; set; }
    public bool EligibleOnly { get; set; } = true;
}

public sealed class BenefitComparisonRequest
{
    public IReadOnlyList<string> PlanCodes { get; set; } = [];
}

public sealed record BenefitComparisonRowDto(
    string FeatureCode,
    string FeatureName,
    IReadOnlyDictionary<string, string> ValuesByPlanCode);

public sealed record BenefitComparisonDto(
    IReadOnlyList<BenefitPlanDto> Plans,
    IReadOnlyList<BenefitComparisonRowDto> Rows);

public sealed class SubmitBenefitEnrollmentRequest
{
    [Required, StringLength(40)] public string PlanCode { get; set; } = string.Empty;
    public IReadOnlyList<string> DependantIds { get; set; } = [];
    [StringLength(2000)] public string? Comment { get; set; }
    public IReadOnlyList<string> EvidenceUploadIds { get; set; } = [];
}

public sealed record BenefitEnrollmentResultDto(
    string EnrollmentId,
    string PlanCode,
    StatusDto Status,
    DateOnly? CoverageFrom,
    MoneyDto? EmployeePremium,
    IReadOnlyList<string> Warnings);
