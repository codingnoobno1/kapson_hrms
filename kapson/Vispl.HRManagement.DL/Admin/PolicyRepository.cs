using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class PolicyRepository(HrManagementDbContext dbContext) : IPolicyRepository
{
    public async Task<IReadOnlyList<PolicyRecord>> GetAllAsync(
        string tenantKey, CancellationToken cancellationToken = default) =>
        (await dbContext.Policies.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey)
            .OrderBy(item => item.Title)
            .ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<IReadOnlyList<PolicyRecord>> GetPublishedAsync(
        string tenantKey, CancellationToken cancellationToken = default) =>
        (await dbContext.Policies.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.IsPublished)
            .OrderBy(item => item.Title)
            .ToListAsync(cancellationToken))
        .Select(ToRecord).ToList();

    public async Task<PolicyRecord?> GetAsync(
        string tenantKey, long policyId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Policies.AsNoTracking()
            .SingleOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == policyId, cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task<long> UpsertAsync(PolicyRecord policy, CancellationToken cancellationToken = default)
    {
        var entity = policy.Id == 0
            ? null
            : await dbContext.Policies.SingleOrDefaultAsync(
                item => item.Id == policy.Id && item.TenantKey == policy.TenantKey, cancellationToken);

        if (entity is null)
        {
            entity = new PolicyEntity();
            dbContext.Policies.Add(entity);
        }

        entity.TenantKey = policy.TenantKey;
        entity.Code = policy.Code;
        entity.Title = policy.Title;
        entity.Icon = policy.Icon;
        entity.Excerpt = policy.Excerpt;
        entity.SectionsJson = policy.SectionsJson;
        entity.Version = policy.Version;
        entity.IsPublished = policy.IsPublished;
        entity.UpdatedOn = policy.UpdatedOn;
        entity.UpdatedUtc = policy.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    /// <summary>
    /// Acknowledgements go with the policy. Leaving them behind would orphan rows that can never
    /// be read again, and would silently re-attach to a future policy if an id were ever reused.
    /// </summary>
    public async Task<bool> DeleteAsync(
        string tenantKey, long policyId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Policies.SingleOrDefaultAsync(
            item => item.TenantKey == tenantKey && item.Id == policyId, cancellationToken);

        if (entity is null)
        {
            return false;
        }

        var acknowledgements = await dbContext.PolicyAcknowledgements
            .Where(item => item.TenantKey == tenantKey && item.PolicyId == policyId)
            .ToListAsync(cancellationToken);

        dbContext.PolicyAcknowledgements.RemoveRange(acknowledgements);
        dbContext.Policies.Remove(entity);

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyDictionary<long, PolicyAcknowledgementRecord>> GetAcknowledgementsAsync(
        string tenantKey, string employeeId, CancellationToken cancellationToken = default) =>
        (await dbContext.PolicyAcknowledgements.AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .ToListAsync(cancellationToken))
        .ToDictionary(
            item => item.PolicyId,
            item => new PolicyAcknowledgementRecord
            {
                TenantKey = item.TenantKey,
                EmployeeId = item.EmployeeId,
                PolicyId = item.PolicyId,
                Version = item.Version,
                AcknowledgedUtc = item.AcknowledgedUtc
            });

    /// <summary>
    /// One row per person per policy: reading a new version replaces the old acknowledgement
    /// rather than appending, so "has this person read the current text" stays a single lookup.
    /// </summary>
    public async Task AcknowledgeAsync(
        PolicyAcknowledgementRecord acknowledgement, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PolicyAcknowledgements.SingleOrDefaultAsync(
            item => item.TenantKey == acknowledgement.TenantKey
                    && item.EmployeeId == acknowledgement.EmployeeId
                    && item.PolicyId == acknowledgement.PolicyId,
            cancellationToken);

        if (entity is null)
        {
            entity = new PolicyAcknowledgementEntity
            {
                TenantKey = acknowledgement.TenantKey,
                EmployeeId = acknowledgement.EmployeeId,
                PolicyId = acknowledgement.PolicyId
            };

            dbContext.PolicyAcknowledgements.Add(entity);
        }

        entity.Version = acknowledgement.Version;
        entity.AcknowledgedUtc = acknowledgement.AcknowledgedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PolicyRecord ToRecord(PolicyEntity entity) => new()
    {
        Id = entity.Id,
        TenantKey = entity.TenantKey,
        Code = entity.Code,
        Title = entity.Title,
        Icon = entity.Icon,
        Excerpt = entity.Excerpt,
        SectionsJson = entity.SectionsJson,
        Version = entity.Version,
        IsPublished = entity.IsPublished,
        UpdatedOn = entity.UpdatedOn,
        UpdatedUtc = entity.UpdatedUtc
    };
}
