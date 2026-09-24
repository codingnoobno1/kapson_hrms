using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

public interface IAccessRoleService
{
    Task<IReadOnlyList<AccessRoleDto>> GetAllAsync(
        string tenantKey,
        AccountPortal? portal = null,
        CancellationToken cancellationToken = default);

    Task<AccessRoleDto> SaveAsync(
        SaveAccessRoleRequest request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string roleId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    IReadOnlyList<PermissionDefinitionDto> GetPermissionCatalog(AccountPortal portal);
}
