using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

public static class SalaryComponentKinds
{
    public const string Allowance = "Allowance";
    public const string Deduction = "Deduction";
}

public static class SalaryCalculationTypes
{
    public const string Fixed = "Fixed";
    public const string PercentOfBasic = "PercentOfBasic";

    // Deductions only: an allowance can't be a share of the gross it is part of.
    public const string PercentOfGross = "PercentOfGross";
}

public sealed class GradeSalaryComponentDto
{
    [Required] public string Kind { get; set; } = SalaryComponentKinds.Allowance;
    [Required] public string ComponentCode { get; set; } = string.Empty;
    public string? ComponentName { get; set; }
    [Required] public string CalculationType { get; set; } = SalaryCalculationTypes.Fixed;
    [Range(0, 100_000_000)] public decimal Value { get; set; }
    [Range(0, 100_000_000)] public decimal? CapAmount { get; set; }

    // Deductions only: not applied when monthly gross is above this, e.g. ESI above ₹21,000.
    [Range(0, 100_000_000)] public decimal? GrossCeiling { get; set; }
}

public sealed class GradeSalaryStructureDto
{
    [Required] public string GradeCode { get; set; } = string.Empty;
    public string? GradeName { get; set; }
    [Range(0, 100_000_000)] public decimal BasicMonthly { get; set; }
    public List<GradeSalaryComponentDto> Components { get; set; } = [];
    public bool IsConfigured { get; set; }
    public DateTimeOffset? UpdatedUtc { get; set; }
}

public sealed record SalaryLineDto(string Code, string Name, decimal Amount, string? Note);

public sealed record SalaryBreakdownDto(
    decimal Basic,
    IReadOnlyList<SalaryLineDto> Allowances,
    decimal Gross,
    IReadOnlyList<SalaryLineDto> Deductions,
    decimal TotalDeductions,
    decimal NetPayable);
