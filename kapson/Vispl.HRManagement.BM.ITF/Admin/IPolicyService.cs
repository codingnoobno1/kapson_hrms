using Vispl.HRManagement.Contracts.Mobile.Dashboard;

namespace Vispl.HRManagement.BM.ITF.Admin;

/// <summary>
/// Company policies (Leave Policy, Attendance Policy, Code of Conduct, …) shown on the HR
/// dashboard's "Company Policies &amp; Leave Rules" panel, and managed from the Admin portal.
///
/// Same shape as <see cref="IMasterDataService"/> deliberately: tenant-scoped primitives, no
/// caller-context dependency, so this interface has no reason to reference the mobile caller type
/// and can be called equally from Admin and from the HR dashboard service.
/// </summary>
public interface IPolicyService
{
    /// <summary>Every policy for this client, published or draft — the Admin editor's list.</summary>
    Task<IReadOnlyList<PolicySummaryDto>> GetAllForAdminAsync(string tenantKey, CancellationToken cancellationToken = default);

    Task<PolicyDetailDto?> GetDetailForAdminAsync(string tenantKey, string policyId, CancellationToken cancellationToken = default);

    /// <summary>Creates or updates a policy. Returns its id. Content changes bump the version.</summary>
    Task<string> SaveAsync(string tenantKey, SavePolicyRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a policy and its acknowledgements. Returns false when there was nothing to delete,
    /// so a double-submit reads as "already gone" rather than failing.
    /// </summary>
    Task<bool> DeleteAsync(string tenantKey, string policyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Published policies only, with this employee's own acknowledgement state resolved against
    /// each one's current version. <paramref name="employeeId"/> is null for a caller with no
    /// employee record (e.g. a platform account) — every policy then reads as unacknowledged,
    /// since there is nobody to have acknowledged it.
    /// </summary>
    Task<IReadOnlyList<PolicySummaryDto>> GetPublishedAsync(string tenantKey, string? employeeId, CancellationToken cancellationToken = default);

    Task<PolicyDetailDto?> GetPublishedDetailAsync(string tenantKey, string? employeeId, string policyId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records that <paramref name="employeeId"/> has read <paramref name="version"/> of this
    /// policy. An acknowledgement of a version that is no longer current is accepted (it is still
    /// true history) but will not make <see cref="PolicySummaryDto.IsAcknowledged"/> true, since
    /// that is always judged against the policy's current version.
    /// </summary>
    Task AcknowledgeAsync(string tenantKey, string employeeId, string policyId, string version, CancellationToken cancellationToken = default);
}
