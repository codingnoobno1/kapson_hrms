using System.Globalization;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.Admin;

// The one place a grade's salary structure becomes rupees; payroll generation reuses it.
public static class SalaryCalculator
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

    // names is keyed "Kind:Code", e.g. "Deduction:PF".
    public static SalaryBreakdownDto Calculate(
        GradeSalaryStructureDto structure, IReadOnlyDictionary<string, string> names)
    {
        var basic = Round(structure.BasicMonthly);

        var allowances = new List<SalaryLineDto>();
        foreach (var item in structure.Components.Where(item => item.Kind == SalaryComponentKinds.Allowance))
        {
            var name = NameOf(item, names);

            if (item.CalculationType == SalaryCalculationTypes.PercentOfGross)
            {
                allowances.Add(new SalaryLineDto(item.ComponentCode, name, 0, "% of Gross isn't allowed for an allowance"));
                continue;
            }

            var raw = item.CalculationType == SalaryCalculationTypes.PercentOfBasic
                ? basic * item.Value / 100m
                : item.Value;

            allowances.Add(Line(item, name, raw));
        }

        var gross = basic + allowances.Sum(line => line.Amount);

        var deductions = new List<SalaryLineDto>();
        foreach (var item in structure.Components.Where(item => item.Kind == SalaryComponentKinds.Deduction))
        {
            var name = NameOf(item, names);

            if (item.GrossCeiling is { } ceiling && gross > ceiling)
            {
                deductions.Add(new SalaryLineDto(item.ComponentCode, name, 0, $"Not applied — gross above {Rupees(ceiling)}"));
                continue;
            }

            var raw = item.CalculationType switch
            {
                SalaryCalculationTypes.PercentOfBasic => basic * item.Value / 100m,
                SalaryCalculationTypes.PercentOfGross => gross * item.Value / 100m,
                _ => item.Value
            };

            deductions.Add(Line(item, name, raw));
        }

        var totalDeductions = deductions.Sum(line => line.Amount);

        return new SalaryBreakdownDto(basic, allowances, gross, deductions, totalDeductions, gross - totalDeductions);
    }

    public static string Rupees(decimal amount) => string.Format(India, "₹{0:N0}", amount);

    private static SalaryLineDto Line(GradeSalaryComponentDto item, string name, decimal raw)
    {
        var amount = Round(raw);

        return item.CapAmount is { } cap && amount > cap
            ? new SalaryLineDto(item.ComponentCode, name, Round(cap), $"Capped at {Rupees(cap)}")
            : new SalaryLineDto(item.ComponentCode, name, amount, null);
    }

    private static string NameOf(GradeSalaryComponentDto item, IReadOnlyDictionary<string, string> names) =>
        string.IsNullOrWhiteSpace(item.ComponentCode)
            ? "Not selected"
            : names.GetValueOrDefault($"{item.Kind}:{item.ComponentCode}") ?? item.ComponentCode;

    private static decimal Round(decimal amount) => Math.Round(amount, 0, MidpointRounding.AwayFromZero);
}
