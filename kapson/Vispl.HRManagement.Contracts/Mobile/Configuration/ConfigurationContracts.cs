namespace Vispl.HRManagement.Contracts.Mobile.Configuration;

public sealed record MobileThemeDto(
    string? LogoUrl,
    string PrimaryColor,
    string AccentColor,
    string SurfaceColor,
    string BackgroundColor,
    string TextColor,
    string ThemeMode);

public sealed record MobileModuleDto(
    string Code,
    string Name,
    bool IsEnabled,
    string? HomeRoute,
    string? RequiredPermission,
    string? UnavailableMessage);

public sealed record MobileNavigationItemDto(
    string Key,
    string Label,
    string Icon,
    string Route,
    int SortOrder,
    string? RequiredPermission,
    string? ModuleCode,
    string? BadgeSource);

public sealed record MobileNavigationDto(
    string Persona,
    string HomeRoute,
    IReadOnlyList<MobileNavigationItemDto> BottomTabs,
    IReadOnlyList<MobileNavigationItemDto> FlyoutItems);

public sealed record MobileDashboardWidgetDto(
    string InstanceId,
    string WidgetKey,
    string Title,
    string Size,
    int SortOrder,
    string? TargetRoute,
    string? RequiredPermission);

public sealed record MobileDashboardLayoutDto(
    string Persona,
    IReadOnlyList<MobileDashboardWidgetDto> Widgets);

public sealed record MobileSecurityPolicyDto(
    bool DeviceRegistrationRequired,
    bool PinRequired,
    bool BiometricAllowed,
    int IdleTimeoutMinutes,
    int MaximumRegisteredDevices,
    bool ScreenshotProtectionRequired);

public sealed record MobileOfflinePolicyDto(
    bool IsEnabled,
    long MaximumCacheBytes,
    int DefaultReadTtlMinutes,
    IReadOnlyList<string> ApprovedReadAreas,
    IReadOnlyList<string> ApprovedOutboxActions);

public sealed record MobileReleasePolicyDto(
    string MinimumVersion,
    string RecommendedVersion,
    bool ForceUpdate,
    string? AndroidStoreUrl,
    string? IosStoreUrl,
    string? MaintenanceMessage);

public sealed record MobileSupportConfigurationDto(
    string? SupportEmail,
    string? SupportUrl,
    string? PrivacyUrl,
    string? TermsUrl);

public sealed record MobileConfigurationDto(
    long Version,
    string TenantKey,
    string TenantName,
    MobileThemeDto Theme,
    IReadOnlyList<MobileModuleDto> Modules,
    MobileNavigationDto Navigation,
    MobileDashboardLayoutDto Dashboard,
    MobileSecurityPolicyDto Security,
    MobileOfflinePolicyDto Offline,
    MobileReleasePolicyDto Release,
    MobileSupportConfigurationDto Support);

public sealed record MasterOptionMobileDto(
    long Id,
    string Category,
    string Code,
    string Name,
    int SortOrder);

public sealed record MasterDataBundleDto(
    long Version,
    DateTimeOffset GeneratedUtc,
    IReadOnlyDictionary<string, IReadOnlyList<MasterOptionMobileDto>> Categories);
