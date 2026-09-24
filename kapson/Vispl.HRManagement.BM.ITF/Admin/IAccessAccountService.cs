using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

public interface IAccessAccountService
{
    Task<IReadOnlyList<AccessAccountDto>> GetAllAsync(
        string tenantKey,
        AccountPortal? portal = null,
        CancellationToken cancellationToken = default);

    Task<AccessAccountDto> CreateAsync(
        CreateAccessAccountRequest request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<AccessAccountDto> UpdateAsync(
        UpdateAccessAccountRequest request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves an account to another portal, resetting its credential and its roles. Update
    /// cannot do this on purpose: changing portal changes what the account is, not what it says.
    /// </summary>
    Task<AccessAccountDto> TransferPortalAsync(
        TransferAccountPortalRequest request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Turns mobile sign-in on or off for one account, leaving <c>IsActive</c> alone.
    ///
    /// Separate from <see cref="SetActiveAsync"/> because they are different decisions: taking
    /// somebody's phone access away should not lock them out of the web portal too.
    /// </summary>
    Task SetMobileAccessAsync(
        string accountId,
        bool enabled,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task SetActiveAsync(
        string accountId,
        bool isActive,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task UnlockAsync(
        string accountId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task ResetPasswordAsync(
        ResetAccountPasswordRequest request,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);
}
