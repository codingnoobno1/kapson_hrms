using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.BM.Admin;

public sealed class GradeSalaryService(
    IGradeSalaryRepository repository,
    IMasterOptionRepository masters) : IGradeSalaryService
{
    // Basic is the grade's own field; linking the BASIC allowance as well would count it twice.
    private const string BasicAllowanceCode = "BASIC";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<GradeSalaryStructureDto>> GetAllAsync(
        string tenantKey, CancellationToken cancellationToken = default)
    {
        var tenant = Normalize(tenantKey);
        var options = await masters.GetAllAsync(tenant, cancellationToken);
        var names = ComponentNames(options);

        var stored = (await repository.GetAllAsync(tenant, cancellationToken))
            .ToDictionary(item => item.GradeCode, StringComparer.OrdinalIgnoreCase);

        return options
            .Where(option => option.Category == MasterCategories.Grade && (option.IsActive || stored.ContainsKey(option.Code)))
            .OrderBy(option => option.SortOrder)
            .ThenBy(option => option.Name)
            .Select(grade =>
            {
                stored.TryGetValue(grade.Code, out var record);

                return new GradeSalaryStructureDto
                {
                    GradeCode = grade.Code,
                    GradeName = grade.Name,
                    BasicMonthly = record?.BasicMonthly ?? 0,
                    Components = record is null ? [] : ReadComponents(record.ComponentsJson, names),
                    IsConfigured = record is not null,
                    UpdatedUtc = record?.UpdatedUtc
                };
            })
            .ToList();
    }

    public async Task SaveAsync(
        string tenantKey, GradeSalaryStructureDto structure, CancellationToken cancellationToken = default)
    {
        var tenant = Normalize(tenantKey);
        var options = await masters.GetAllAsync(tenant, cancellationToken);

        var grade = options.FirstOrDefault(option =>
                        option.Category == MasterCategories.Grade
                        && string.Equals(option.Code, structure.GradeCode?.Trim(), StringComparison.OrdinalIgnoreCase))
                    ?? throw new ArgumentException("Choose a grade from the Grade master.");

        if (structure.BasicMonthly < 0)
        {
            throw new ArgumentException("Basic salary can't be negative.");
        }

        var existing = await repository.GetAsync(tenant, grade.Code, cancellationToken);

        // A component retired from its master since it was linked may stay; a new link must be active.
        var previouslyLinked = existing is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : ReadComponents(existing.ComponentsJson, null)
                .Select(item => $"{item.Kind}:{item.ComponentCode}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cleaned = new List<GradeSalaryComponentDto>();

        foreach (var item in structure.Components)
        {
            if (item.Kind is not (SalaryComponentKinds.Allowance or SalaryComponentKinds.Deduction))
            {
                throw new ArgumentException($"'{item.Kind}' isn't an allowance or a deduction.");
            }

            var isDeduction = item.Kind == SalaryComponentKinds.Deduction;
            var label = isDeduction ? "deduction" : "allowance";
            var category = isDeduction ? MasterCategories.DeductionType : MasterCategories.AllowanceType;
            var code = item.ComponentCode?.Trim() ?? string.Empty;

            if (code.Length == 0)
            {
                throw new ArgumentException($"Choose a component for every {label} row.");
            }

            if (!isDeduction && string.Equals(code, BasicAllowanceCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Basic salary is set on the grade itself, so it can't also be linked as an allowance.");
            }

            var option = options.FirstOrDefault(candidate =>
                candidate.Category == category && string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase));

            var key = $"{item.Kind}:{code}";

            if (option is null || (!option.IsActive && !previouslyLinked.Contains(key)))
            {
                throw new ArgumentException($"'{code}' isn't an active {label} in the {label} master.");
            }

            if (!seen.Add(key))
            {
                throw new ArgumentException($"{option.Name} is linked more than once.");
            }

            if (item.CalculationType is not (SalaryCalculationTypes.Fixed or SalaryCalculationTypes.PercentOfBasic or SalaryCalculationTypes.PercentOfGross))
            {
                throw new ArgumentException($"Choose how {option.Name} is calculated.");
            }

            if (!isDeduction && item.CalculationType == SalaryCalculationTypes.PercentOfGross)
            {
                throw new ArgumentException($"{option.Name} can't be a percentage of gross, because gross is made up of the allowances.");
            }

            if (item.Value < 0 || item.CapAmount < 0 || item.GrossCeiling < 0)
            {
                throw new ArgumentException($"{option.Name} can't have a negative amount.");
            }

            if (item.CalculationType != SalaryCalculationTypes.Fixed && item.Value > 100)
            {
                throw new ArgumentException($"{option.Name} can't be more than 100%.");
            }

            cleaned.Add(new GradeSalaryComponentDto
            {
                Kind = item.Kind,
                ComponentCode = option.Code,
                CalculationType = item.CalculationType,
                Value = item.Value,
                CapAmount = item.CapAmount,
                GrossCeiling = isDeduction ? item.GrossCeiling : null
            });
        }

        await repository.UpsertAsync(new GradeSalaryRecord
        {
            TenantKey = tenant,
            GradeCode = grade.Code,
            BasicMonthly = structure.BasicMonthly,
            ComponentsJson = JsonSerializer.Serialize(cleaned, Json),
            UpdatedUtc = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    public async Task<SalaryBreakdownDto> CalculateAsync(
        string tenantKey, GradeSalaryStructureDto structure, CancellationToken cancellationToken = default)
    {
        var options = await masters.GetAllAsync(Normalize(tenantKey), cancellationToken);
        return SalaryCalculator.Calculate(structure, ComponentNames(options));
    }

    private static Dictionary<string, string> ComponentNames(IReadOnlyList<MasterOptionRecord> options) =>
        options
            .Where(option => option.Category is MasterCategories.AllowanceType or MasterCategories.DeductionType)
            .GroupBy(
                option => $"{(option.Category == MasterCategories.AllowanceType ? SalaryComponentKinds.Allowance : SalaryComponentKinds.Deduction)}:{option.Code}",
                StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

    private static List<GradeSalaryComponentDto> ReadComponents(string json, IReadOnlyDictionary<string, string>? names)
    {
        List<GradeSalaryComponentDto> items;

        try
        {
            items = JsonSerializer.Deserialize<List<GradeSalaryComponentDto>>(json, Json) ?? [];
        }
        catch (JsonException)
        {
            items = [];
        }

        if (names is not null)
        {
            foreach (var item in items)
            {
                item.ComponentName = names.GetValueOrDefault($"{item.Kind}:{item.ComponentCode}");
            }
        }

        return items;
    }

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToLowerInvariant();
    }
}
