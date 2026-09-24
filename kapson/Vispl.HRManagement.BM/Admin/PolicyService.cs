using System.Globalization;
using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Mobile.Dashboard;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.BM.Admin;

public sealed class PolicyService(IPolicyRepository repository) : IPolicyService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<PolicySummaryDto>> GetAllForAdminAsync(
        string tenantKey, CancellationToken cancellationToken = default) =>
        (await repository.GetAllAsync(Normalize(tenantKey), cancellationToken))
        .Select(policy => ToSummary(policy, acknowledgement: null))
        .ToList();

    public async Task<PolicyDetailDto?> GetDetailForAdminAsync(
        string tenantKey, string policyId, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(policyId, out var id))
        {
            return null;
        }

        var policy = await repository.GetAsync(Normalize(tenantKey), id, cancellationToken);
        return policy is null ? null : new PolicyDetailDto(ToSummary(policy, null), ReadSections(policy));
    }

    /// <summary>
    /// The version bumps only when the text a reader would see actually changes. Renaming nothing
    /// and re-saving must not un-acknowledge the whole company, and changing a clause must.
    /// </summary>
    public async Task<string> SaveAsync(
        string tenantKey, SavePolicyRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = Normalize(tenantKey);
        var sectionsJson = JsonSerializer.Serialize(request.Sections, Json);

        var existing = long.TryParse(request.Id, out var id) && id != 0
            ? await repository.GetAsync(tenant, id, cancellationToken)
            : null;

        var contentChanged = existing is null
            || existing.Title != request.Title.Trim()
            || existing.Excerpt != request.Excerpt.Trim()
            || existing.SectionsJson != sectionsJson;

        var version = existing is null
            ? "1"
            : contentChanged
                ? NextVersion(existing.Version)
                : existing.Version;

        var saved = await repository.UpsertAsync(new PolicyRecord
        {
            Id = existing?.Id ?? 0,
            TenantKey = tenant,
            Code = request.Code.Trim().ToUpperInvariant(),
            Title = request.Title.Trim(),
            Icon = string.IsNullOrWhiteSpace(request.Icon) ? "📄" : request.Icon.Trim(),
            Excerpt = request.Excerpt.Trim(),
            SectionsJson = sectionsJson,
            Version = version,
            IsPublished = request.IsPublished,
            UpdatedOn = contentChanged || existing is null
                ? DateOnly.FromDateTime(DateTime.UtcNow)
                : existing.UpdatedOn,
            UpdatedUtc = DateTimeOffset.UtcNow
        }, cancellationToken);

        return saved.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<bool> DeleteAsync(
        string tenantKey, string policyId, CancellationToken cancellationToken = default) =>
        long.TryParse(policyId, out var id)
        && await repository.DeleteAsync(Normalize(tenantKey), id, cancellationToken);

    public async Task<IReadOnlyList<PolicySummaryDto>> GetPublishedAsync(
        string tenantKey, string? employeeId, CancellationToken cancellationToken = default)
    {
        var tenant = Normalize(tenantKey);
        var policies = await repository.GetPublishedAsync(tenant, cancellationToken);

        var acknowledgements = string.IsNullOrWhiteSpace(employeeId)
            ? new Dictionary<long, PolicyAcknowledgementRecord>()
            : (IReadOnlyDictionary<long, PolicyAcknowledgementRecord>)
                await repository.GetAcknowledgementsAsync(tenant, employeeId, cancellationToken);

        return policies
            .Select(policy => ToSummary(policy, acknowledgements.GetValueOrDefault(policy.Id)))
            .ToList();
    }

    public async Task<PolicyDetailDto?> GetPublishedDetailAsync(
        string tenantKey, string? employeeId, string policyId, CancellationToken cancellationToken = default)
    {
        if (!long.TryParse(policyId, out var id))
        {
            return null;
        }

        var tenant = Normalize(tenantKey);
        var policy = await repository.GetAsync(tenant, id, cancellationToken);

        if (policy is null || !policy.IsPublished)
        {
            return null;
        }

        var acknowledgement = string.IsNullOrWhiteSpace(employeeId)
            ? null
            : (await repository.GetAcknowledgementsAsync(tenant, employeeId, cancellationToken))
                .GetValueOrDefault(policy.Id);

        return new PolicyDetailDto(ToSummary(policy, acknowledgement), ReadSections(policy));
    }

    public async Task AcknowledgeAsync(
        string tenantKey, string employeeId, string policyId, string version,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(employeeId);

        if (!long.TryParse(policyId, out var id))
        {
            throw new ArgumentException($"'{policyId}' is not a policy id.", nameof(policyId));
        }

        await repository.AcknowledgeAsync(new PolicyAcknowledgementRecord
        {
            TenantKey = Normalize(tenantKey),
            EmployeeId = employeeId,
            PolicyId = id,
            Version = version,
            AcknowledgedUtc = DateTimeOffset.UtcNow
        }, cancellationToken);
    }

    /// <summary>
    /// Acknowledged means "read the text that is on screen now" — an acknowledgement recorded
    /// against an earlier version is kept as history but does not count against the current one.
    /// </summary>
    private static PolicySummaryDto ToSummary(PolicyRecord policy, PolicyAcknowledgementRecord? acknowledgement)
    {
        var isCurrent = acknowledgement is not null && acknowledgement.Version == policy.Version;

        return new PolicySummaryDto(
            policy.Id.ToString(CultureInfo.InvariantCulture),
            policy.Code,
            policy.Title,
            policy.Icon,
            policy.Excerpt,
            policy.Version,
            policy.UpdatedOn,
            policy.IsPublished,
            isCurrent,
            isCurrent ? acknowledgement!.AcknowledgedUtc : null);
    }

    private static IReadOnlyList<PolicySectionDto> ReadSections(PolicyRecord policy)
    {
        try
        {
            return JsonSerializer.Deserialize<List<PolicySectionDto>>(policy.SectionsJson, Json) ?? [];
        }
        catch (JsonException)
        {
            // A policy whose body cannot be parsed still has a title and an excerpt worth showing;
            // losing the card entirely would hide that the policy exists at all.
            return [];
        }
    }

    private static string NextVersion(string current) =>
        int.TryParse(current, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? (parsed + 1).ToString(CultureInfo.InvariantCulture)
            : "1";

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToLowerInvariant();
    }
}
