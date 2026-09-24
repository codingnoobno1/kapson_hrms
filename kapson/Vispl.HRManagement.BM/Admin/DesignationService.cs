using System.Globalization;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.BM.Admin;

public sealed class DesignationService(
    IDesignationRepository repository,
    IMasterOptionRepository masters) : IDesignationService
{
    public async Task<IReadOnlyList<DesignationDto>> GetAllAsync(
        string tenantKey, CancellationToken cancellationToken = default)
    {
        var tenant = Normalize(tenantKey);
        var designations = await repository.GetAllAsync(tenant, cancellationToken);
        var options = await masters.GetAllAsync(tenant, cancellationToken);

        var grades = NameLookup(options, MasterCategories.Grade);
        var departments = NameLookup(options, MasterCategories.Department);

        return designations
            .Select(item => new DesignationDto(
                item.Id.ToString(CultureInfo.InvariantCulture),
                item.Code,
                item.Name,
                item.GradeCode,
                grades.GetValueOrDefault(item.GradeCode),
                item.DepartmentCode,
                departments.GetValueOrDefault(item.DepartmentCode),
                item.Description,
                item.SortOrder,
                item.IsActive,
                item.UpdatedUtc))
            .ToList();
    }

    public async Task<string> SaveAsync(
        string tenantKey, SaveDesignationRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = Normalize(tenantKey);
        var code = request.Code.Trim().ToUpperInvariant();
        var name = request.Name.Trim();

        if (code.Length == 0 || name.Length == 0)
        {
            throw new ArgumentException("A designation needs both a name and a code.");
        }

        var existing = await repository.GetAllAsync(tenant, cancellationToken);
        var id = long.TryParse(request.Id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
        var current = id == 0 ? null : existing.FirstOrDefault(item => item.Id == id);

        if (id != 0 && current is null)
        {
            throw new InvalidOperationException("That designation no longer exists.");
        }

        if (existing.Any(item => item.Id != id && string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Another designation already uses the code '{code}'.");
        }

        // An unchanged grade or department may have been retired since; only a new choice must be active.
        var options = await masters.GetAllAsync(tenant, cancellationToken);
        var gradeCode = ResolveOption(options, MasterCategories.Grade, request.GradeCode, "grade", current?.GradeCode);
        var departmentCode = ResolveOption(options, MasterCategories.Department, request.DepartmentCode, "department", current?.DepartmentCode);

        var saved = await repository.UpsertAsync(new DesignationRecord
        {
            Id = id,
            TenantKey = tenant,
            Code = code,
            Name = name,
            GradeCode = gradeCode,
            DepartmentCode = departmentCode,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            SortOrder = current?.SortOrder ?? (existing.Count == 0 ? 1 : existing.Max(item => item.SortOrder) + 1),
            IsActive = request.IsActive,
            UpdatedUtc = DateTimeOffset.UtcNow
        }, cancellationToken);

        return saved.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<bool> DeleteAsync(
        string tenantKey, string designationId, CancellationToken cancellationToken = default) =>
        long.TryParse(designationId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
        && await repository.DeleteAsync(Normalize(tenantKey), id, cancellationToken);

    public async Task ReorderAsync(
        string tenantKey, IReadOnlyList<string> orderedIds, CancellationToken cancellationToken = default)
    {
        var ids = orderedIds
            .Select(value => long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : 0)
            .Where(id => id != 0)
            .ToList();

        await repository.SetOrderAsync(Normalize(tenantKey), ids, cancellationToken);
    }

    private static string ResolveOption(
        IReadOnlyList<MasterOptionRecord> options, string category, string? requested, string label, string? unchangedCode)
    {
        var code = requested?.Trim() ?? string.Empty;

        var match = options.FirstOrDefault(option =>
            option.Category == category && string.Equals(option.Code, code, StringComparison.OrdinalIgnoreCase));

        var isUnchanged = unchangedCode is not null && string.Equals(unchangedCode, code, StringComparison.OrdinalIgnoreCase);

        if (match is null || (!match.IsActive && !isUnchanged))
        {
            throw new ArgumentException($"Choose an active {label} from the {label} master.");
        }

        return match.Code;
    }

    private static Dictionary<string, string> NameLookup(IReadOnlyList<MasterOptionRecord> options, string category) =>
        options
            .Where(option => option.Category == category)
            .GroupBy(option => option.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Name, StringComparer.OrdinalIgnoreCase);

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToLowerInvariant();
    }
}
