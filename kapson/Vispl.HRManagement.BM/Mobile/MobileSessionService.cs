using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Bootstrap;
using Vispl.HRManagement.Contracts.Mobile.Configuration;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Projects the caller's own session. Everything but the tenant display name comes from the
/// token the caller already holds, so this is a cheap call the app can make on every launch.
///
/// Device registration, offline grants and server-side logout still belong to the modules
/// that own them; those members stay unimplemented rather than pretending to work.
/// </summary>
public sealed class MobileSessionService(ITenantService tenants) : IMobileSessionService
{
    public Task<MobileSessionDto> BootstrapAsync(MobileCallerContext caller, CancellationToken ct) =>
        ProjectAsync(caller, ct);

    public Task<MobileSessionDto> RefreshAsync(MobileCallerContext caller, CancellationToken ct) =>
        ProjectAsync(caller, ct);

    public Task LogoutAsync(MobileCallerContext caller, LogoutRequest request, CancellationToken ct) =>
        // Sign-out revokes the refresh token through the auth endpoint. Device-scoped
        // revocation belongs to the device module, which does not exist yet.
        throw new MobileFeatureNotReadyException(
            "Session logout", "device session entity", "device revocation service");

    public Task<MobileOfflinePolicyDto> GetOfflineGrantAsync(MobileCallerContext caller, CancellationToken ct) =>
        throw new MobileFeatureNotReadyException(
            "Offline grant", "mobile configuration", "offline policy publisher");

    private async Task<MobileSessionDto> ProjectAsync(MobileCallerContext caller, CancellationToken ct)
    {
        var tenant = await tenants.GetByKeyAsync(caller.TenantKey, ct);

        return new MobileSessionDto(
            new SessionUserDto(caller.UserId, caller.EmployeeId, caller.UserId, null, null),
            new SessionTenantDto(
                caller.TenantKey,
                caller.TenantKey,
                tenant?.DisplayName ?? caller.TenantKey,
                null,
                "Asia/Kolkata",
                "en-IN",
                "INR"),
            "Hr",
            [],
            caller.Permissions.ToList(),
            [],
            [],
            DateTimeOffset.UtcNow,
            false);
    }
}
