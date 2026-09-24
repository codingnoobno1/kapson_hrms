using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.BM.Admin;

public sealed class MasterDataService(IMasterOptionRepository repository) : IMasterDataService
{
    public async Task<IReadOnlyList<MasterOptionDto>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default) =>
        (await repository.GetAllAsync(Normalize(tenantKey), cancellationToken)).Select(ToDto).ToList();

    public async Task<IReadOnlyList<MasterOptionDto>> GetActiveAsync(string tenantKey, string category, CancellationToken cancellationToken = default) =>
        (await repository.GetActiveAsync(Normalize(tenantKey), category, cancellationToken)).Select(ToDto).ToList();

    public async Task<long> SaveAsync(MasterOptionDto option, CancellationToken cancellationToken = default)
    {
        if (!MasterCategories.All.Contains(option.Category))
            throw new ArgumentException($"Unknown master category '{option.Category}'.", nameof(option));

        var tenantKey = Normalize(option.TenantKey);
        var parentCode = await ResolveParentAsync(tenantKey, option, cancellationToken);

        return await repository.UpsertAsync(new MasterOptionRecord
        {
            Id = option.Id, TenantKey = tenantKey, Category = option.Category,
            Code = option.Code.Trim().ToUpperInvariant(), Name = option.Name.Trim(), SortOrder = option.SortOrder,
            ParentCode = parentCode,
            Description = string.IsNullOrWhiteSpace(option.Description) ? null : option.Description.Trim(),
            IsActive = option.IsActive, UpdatedUtc = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    /// <summary>
    /// A parent code must name a real value of the parent category (a skill's Skill Category). A
    /// category without a parent never stores one, whatever the caller sent.
    /// </summary>
    private async Task<string?> ResolveParentAsync(string tenantKey, MasterOptionDto option, CancellationToken cancellationToken)
    {
        var parentCategory = MasterCategories.ParentOf(option.Category);
        if (parentCategory is null || string.IsNullOrWhiteSpace(option.ParentCode))
            return null;

        var parentCode = option.ParentCode.Trim().ToUpperInvariant();
        var exists = (await repository.GetAllAsync(tenantKey, cancellationToken))
            .Any(item => item.Category == parentCategory && item.Code == parentCode);

        return exists
            ? parentCode
            : throw new ArgumentException($"'{parentCode}' is not a {MasterCategories.DisplayName(parentCategory)}.", nameof(option));
    }

    private static MasterOptionDto ToDto(MasterOptionRecord item) => new()
    {
        Id = item.Id, TenantKey = item.TenantKey, Category = item.Category, Code = item.Code,
        Name = item.Name, SortOrder = item.SortOrder, ParentCode = item.ParentCode, Description = item.Description,
        IsActive = item.IsActive, UpdatedUtc = item.UpdatedUtc
    };
    private static string Normalize(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim().ToLowerInvariant(); }
}
