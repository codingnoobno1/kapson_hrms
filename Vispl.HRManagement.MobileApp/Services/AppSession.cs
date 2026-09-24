using Vispl.HRManagement.Contracts.Mobile.Bootstrap;

namespace Vispl.HRManagement.MobileApp.Services;

/// <summary>
/// App-wide state: which tenant the device is bound to and who is signed in.
/// </summary>
public sealed class AppSession(ITokenStore tokenStore)
{
    private const string TenantKeyPreference = "vispl.hr.tenant_key";
    private const string TenantNamePreference = "vispl.hr.tenant_name";
    private const string TenantLogoPreference = "vispl.hr.tenant_logo";
    private const string AuthorityPreference = "vispl.hr.authority";
    private const string BaseAddressPreference = "vispl.hr.base_address";
    private const string RefreshTokenPreference = "vispl.hr.refresh_token";
    private const string DeviceIdPreference = "vispl.hr.device_id";
    private const string DisplayNamePreference = "vispl.hr.display_name";
    private const string PortalPreference = "vispl.hr.portal";

    public event Action? Changed;

    public string? TenantKey { get; private set; }
    public string? TenantName { get; private set; }
    public string? TenantLogoUrl { get; private set; }
    public string? Authority { get; private set; }
    public MobileSessionDto? Session { get; private set; }

    public string? DisplayName { get; private set; }
    public string? Portal { get; private set; }
    public IReadOnlyList<string> Permissions { get; private set; } = [];

    /// <summary>Stable per-install identifier, so a token can be revoked for one device.</summary>
    public string DeviceId
    {
        get
        {
            var existing = Preferences.Default.Get<string?>(DeviceIdPreference, null);
            if (!string.IsNullOrWhiteSpace(existing))
            {
                return existing;
            }

            var created = Guid.NewGuid().ToString("N");
            Preferences.Default.Set(DeviceIdPreference, created);
            return created;
        }
    }

    public bool HasTenant => !string.IsNullOrWhiteSpace(TenantKey);
    public bool IsSignedIn => !string.IsNullOrWhiteSpace(DisplayName);

    public void Load()
    {
        TenantKey = Preferences.Default.Get<string?>(TenantKeyPreference, null);
        TenantName = Preferences.Default.Get<string?>(TenantNamePreference, null);
        TenantLogoUrl = Preferences.Default.Get<string?>(TenantLogoPreference, null);
        Authority = Preferences.Default.Get<string?>(AuthorityPreference, null);
        DisplayName = Preferences.Default.Get<string?>(DisplayNamePreference, null);
        Portal = Preferences.Default.Get<string?>(PortalPreference, null);
    }

    /// <summary>
    /// Records a successful sign-in. The access token goes to secure storage; the refresh
    /// token is kept alongside it so a relaunch can renew without asking for the password again.
    /// </summary>
    public async Task SignInAsync(
        string accessToken,
        string refreshToken,
        string displayName,
        string portal,
        IReadOnlyList<string> permissions)
    {
        await tokenStore.SetAccessTokenAsync(accessToken);
        await tokenStore.SetRefreshTokenAsync(refreshToken);

        DisplayName = displayName;
        Portal = portal;
        Permissions = permissions;

        Preferences.Default.Set(DisplayNamePreference, displayName);
        Preferences.Default.Set(PortalPreference, portal);
        Changed?.Invoke();
    }

    public void SetTenant(TenantDiscoveryDto tenant)
    {
        TenantKey = tenant.TenantKey;
        TenantName = tenant.DisplayName;
        TenantLogoUrl = tenant.LogoUrl;
        Authority = tenant.Authority;

        Preferences.Default.Set(TenantKeyPreference, tenant.TenantKey);
        Preferences.Default.Set(TenantNamePreference, tenant.DisplayName);
        Preferences.Default.Set(TenantLogoPreference, tenant.LogoUrl ?? string.Empty);
        Preferences.Default.Set(AuthorityPreference, tenant.Authority);
        Changed?.Invoke();
    }

    public void SetSession(MobileSessionDto? session)
    {
        Session = session;
        Changed?.Invoke();
    }

    public async Task SignOutAsync()
    {
        Session = null;
        DisplayName = null;
        Portal = null;
        Permissions = [];

        Preferences.Default.Remove(DisplayNamePreference);
        Preferences.Default.Remove(PortalPreference);

        await tokenStore.ClearAsync();
        Changed?.Invoke();
    }

    public async Task ForgetTenantAsync()
    {
        Preferences.Default.Remove(TenantKeyPreference);
        Preferences.Default.Remove(TenantNamePreference);
        Preferences.Default.Remove(TenantLogoPreference);
        Preferences.Default.Remove(AuthorityPreference);
        TenantKey = null;
        TenantName = null;
        TenantLogoUrl = null;
        Authority = null;
        await SignOutAsync();
    }

    public string GetBaseAddress(MobileAppOptions options) =>
        Preferences.Default.Get(BaseAddressPreference, options.BaseAddress);

    public void SetBaseAddress(string baseAddress)
    {
        Preferences.Default.Set(BaseAddressPreference, baseAddress);
        Changed?.Invoke();
    }

    public bool HasPermission(string permission) =>
        Permissions.Any(item => string.Equals(item, permission, StringComparison.OrdinalIgnoreCase));
}
