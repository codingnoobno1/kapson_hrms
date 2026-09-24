using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

public interface IDesignationService
{
    Task<IReadOnlyList<DesignationDto>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);

    Task<string> SaveAsync(string tenantKey, SaveDesignationRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(string tenantKey, string designationId, CancellationToken cancellationToken = default);

    // Designations missing from orderedIds keep their relative order after the listed ones.
    Task ReorderAsync(string tenantKey, IReadOnlyList<string> orderedIds, CancellationToken cancellationToken = default);
}
