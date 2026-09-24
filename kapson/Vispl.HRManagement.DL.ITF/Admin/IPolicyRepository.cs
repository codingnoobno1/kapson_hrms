using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

public interface IPolicyRepository
{
    Task<IReadOnlyList<PolicyRecord>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PolicyRecord>> GetPublishedAsync(string tenantKey, CancellationToken cancellationToken = default);

    Task<PolicyRecord?> GetAsync(string tenantKey, long policyId, CancellationToken cancellationToken = default);

    Task<long> UpsertAsync(PolicyRecord policy, CancellationToken cancellationToken = default);

    /// <summary>Removes the policy and every acknowledgement recorded against it.</summary>
    Task<bool> DeleteAsync(string tenantKey, long policyId, CancellationToken cancellationToken = default);

    /// <summary>This employee's acknowledgements, keyed by policy id.</summary>
    Task<IReadOnlyDictionary<long, PolicyAcknowledgementRecord>> GetAcknowledgementsAsync(
        string tenantKey, string employeeId, CancellationToken cancellationToken = default);

    Task AcknowledgeAsync(PolicyAcknowledgementRecord acknowledgement, CancellationToken cancellationToken = default);
}
