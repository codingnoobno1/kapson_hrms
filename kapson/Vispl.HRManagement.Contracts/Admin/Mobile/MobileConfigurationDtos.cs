using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin.Mobile;

public sealed class MobileAppConfigurationAdminDto
{
    [Required, RegularExpression("^[a-z0-9-]+$")]
    public string TenantKey { get; set; } = string.Empty;

    public long Version { get; set; }
    public MobileAppIdentityAdminDto App { get; set; } = new();
    public MobileBrandingAdminDto Branding { get; set; } = new();
    public List<MobileModuleAdminDto> Modules { get; set; } = [];
    public List<MobilePersonaNavigationAdminDto> Navigation { get; set; } = [];
    public List<MobileDashboardAdminDto> Dashboards { get; set; } = [];
    public List<MobileQuickActionGroupAdminDto> QuickActions { get; set; } = [];
    public MobileSecurityAdminDto Security { get; set; } = new();
    public MobileOfflineAdminDto Offline { get; set; } = new();
    public MobileNotificationAdminDto Notifications { get; set; } = new();
    public List<MobileDeepLinkAdminDto> DeepLinks { get; set; } = [];
    public MobileSupportAdminDto Support { get; set; } = new();
}

public sealed class MobileAppIdentityAdminDto
{
    [Required, StringLength(80)] public string DisplayName { get; set; } = "HR Management";
    [Required, StringLength(30)] public string ShortName { get; set; } = "HRM";
    [Required, StringLength(20)] public string DefaultLocale { get; set; } = "en-IN";
    [Required, StringLength(100)] public string TimeZoneId { get; set; } = "Asia/Kolkata";
    [Required, StringLength(3, MinimumLength = 3)] public string CurrencyCode { get; set; } = "INR";
}

public sealed class MobileBrandingAdminDto
{
    public long? LightLogoMediaId { get; set; }
    public long? DarkLogoMediaId { get; set; }
    public long? SplashMediaId { get; set; }
    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")] public string PrimaryColor { get; set; } = "#132342";
    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")] public string AccentColor { get; set; } = "#E3B13D";
    [Required, RegularExpression("^#[0-9A-Fa-f]{6}$")] public string SurfaceColor { get; set; } = "#FFFFFF";
    [Required, StringLength(80)] public string FontFamilyKey { get; set; } = "system";
    [Required, RegularExpression("^(system|light|dark)$")] public string ThemeModePolicy { get; set; } = "system";
}

public sealed class MobileModuleAdminDto
{
    [Required, StringLength(60)] public string Code { get; set; } = string.Empty;
    public bool IsLicensed { get; set; }
    public bool IsTenantEnabled { get; set; }
    public DateTimeOffset? EffectiveFromUtc { get; set; }
    public DateTimeOffset? EffectiveToUtc { get; set; }
    [StringLength(300)] public string? UnavailableMessage { get; set; }
}

public sealed class MobilePersonaNavigationAdminDto
{
    [Required, StringLength(60)] public string Persona { get; set; } = string.Empty;
    [Required, StringLength(100)] public string HomeRouteKey { get; set; } = string.Empty;
    public List<MobileNavigationItemAdminDto> BottomTabs { get; set; } = [];
    public List<MobileNavigationItemAdminDto> FlyoutItems { get; set; } = [];
}

public sealed class MobileNavigationItemAdminDto
{
    [Required, StringLength(80)] public string Key { get; set; } = string.Empty;
    [Required, StringLength(80)] public string Label { get; set; } = string.Empty;
    [Required, StringLength(100)] public string RouteKey { get; set; } = string.Empty;
    [Required, StringLength(80)] public string IconKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    [StringLength(100)] public string? RequiredPermission { get; set; }
}

public sealed class MobileDashboardAdminDto
{
    [Required, StringLength(60)] public string Persona { get; set; } = string.Empty;
    public List<MobileDashboardWidgetAdminDto> Widgets { get; set; } = [];
}

public sealed class MobileDashboardWidgetAdminDto
{
    [Required, StringLength(100)] public string InstanceId { get; set; } = string.Empty;
    [Required, StringLength(100)] public string WidgetKey { get; set; } = string.Empty;
    [StringLength(100)] public string? TitleOverride { get; set; }
    [Required, RegularExpression("^(small|medium|large|full)$")] public string Size { get; set; } = "medium";
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;
}

public sealed class MobileQuickActionGroupAdminDto
{
    [Required, StringLength(60)] public string Persona { get; set; } = string.Empty;
    public int MaximumCount { get; set; } = 6;
    public List<MobileQuickActionAdminDto> Actions { get; set; } = [];
}

public sealed class MobileQuickActionAdminDto
{
    [Required, StringLength(100)] public string ActionKey { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsEnabled { get; set; } = true;
    public bool CanUserPin { get; set; }
}

public sealed class MobileSecurityAdminDto
{
    public bool DeviceRegistrationRequired { get; set; } = true;
    public bool PinRequired { get; set; }
    public bool BiometricAllowed { get; set; } = true;
    [Range(1, 1440)] public int IdleTimeoutMinutes { get; set; } = 30;
    [Range(1, 20)] public int MaximumRegisteredDevices { get; set; } = 5;
    public bool ScreenshotProtectionRequired { get; set; } = true;
    public bool BlockCompromisedDevices { get; set; } = true;
}

public sealed class MobileOfflineAdminDto
{
    public bool IsEnabled { get; set; }
    [Range(0, 1_073_741_824)] public long MaximumCacheBytes { get; set; } = 52_428_800;
    [Range(1, 10080)] public int DefaultReadTtlMinutes { get; set; } = 60;
    public List<string> ApprovedReadAreas { get; set; } = [];
    public List<string> ApprovedOutboxActions { get; set; } = [];
    public bool WipeOnSignOut { get; set; } = true;
    public bool WipeOnRoleChange { get; set; } = true;
}

public sealed class MobileNotificationAdminDto
{
    public bool PushEnabled { get; set; }
    public string? ProviderReferenceKey { get; set; }
    public List<MobileNotificationEventAdminDto> Events { get; set; } = [];
}

public sealed class MobileNotificationEventAdminDto
{
    [Required, StringLength(100)] public string EventKey { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public bool IsMandatory { get; set; }
    [Required, StringLength(160)] public string TitleTemplate { get; set; } = string.Empty;
    [Required, StringLength(500)] public string BodyTemplate { get; set; } = string.Empty;
    [StringLength(100)] public string? DeepLinkKey { get; set; }
}

public sealed class MobileDeepLinkAdminDto
{
    [Required, StringLength(100)] public string Key { get; set; } = string.Empty;
    [Required, StringLength(500)] public string UriPattern { get; set; } = string.Empty;
    [Required, StringLength(100)] public string RouteKey { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class MobileSupportAdminDto
{
    [EmailAddress, StringLength(240)] public string? SupportEmail { get; set; }
    [Url, StringLength(500)] public string? SupportUrl { get; set; }
    [Url, StringLength(500)] public string? PrivacyUrl { get; set; }
    [Url, StringLength(500)] public string? TermsUrl { get; set; }
}

public sealed record MobileAdminOverviewDto(
    string TenantKey,
    long DraftBasedOnVersion,
    long PublishedVersion,
    bool HasDraftChanges,
    int EnabledModuleCount,
    int ValidationErrorCount,
    DateTimeOffset? PublishedUtc);

public sealed record MobileConfigurationEditorDto(
    MobileAppConfigurationAdminDto Draft,
    long DraftBasedOnVersion,
    string RowVersion,
    DateTimeOffset UpdatedUtc,
    string? UpdatedByUserId);

public sealed class SaveMobileConfigurationDraftRequest
{
    [Required, RegularExpression("^[a-z0-9-]+$")] public string TenantKey { get; set; } = string.Empty;
    [Required] public MobileAppConfigurationAdminDto Configuration { get; set; } = new();
    [Required, StringLength(200)] public string RowVersion { get; set; } = string.Empty;
    [StringLength(500)] public string? Comment { get; set; }
}

public sealed record MobileConfigurationValidationIssueDto(
    string Severity,
    string Code,
    string Path,
    string Message);

public sealed record MobileConfigurationValidationDto(
    bool IsValid,
    string ValidationToken,
    IReadOnlyList<MobileConfigurationValidationIssueDto> Issues);

public sealed class PublishMobileConfigurationRequest
{
    [Required, RegularExpression("^[a-z0-9-]+$")] public string TenantKey { get; set; } = string.Empty;
    [Required, StringLength(200)] public string RowVersion { get; set; } = string.Empty;
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(500)] public string ValidationToken { get; set; } = string.Empty;
}

public sealed record PublishMobileConfigurationResultDto(
    string TenantKey,
    long PublishedVersion,
    string Checksum,
    DateTimeOffset PublishedUtc,
    bool CacheInvalidationQueued);

public sealed record MobileConfigurationVersionDto(
    long Version,
    string Action,
    string ActorDisplayName,
    DateTimeOffset OccurredUtc,
    string Reason,
    string Checksum,
    long? SourceVersion);

public sealed class RollbackMobileConfigurationRequest
{
    [Required, RegularExpression("^[a-z0-9-]+$")] public string TenantKey { get; set; } = string.Empty;
    [Range(1, long.MaxValue)] public long SourceVersion { get; set; }
    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
    [Range(1, long.MaxValue)] public long ExpectedCurrentVersion { get; set; }
}

public sealed record MobileReleasePolicyAdminDto(
    string Platform,
    string Channel,
    string MinimumVersion,
    string RecommendedVersion,
    string LatestVersion,
    bool ForceUpdate,
    DateTimeOffset? EnforceAfterUtc,
    int RolloutPercentage,
    string StoreUrl,
    string? RowVersion);

public sealed record MobileMaintenanceWindowAdminDto(
    string Id,
    string? TenantKey,
    DateTimeOffset StartsUtc,
    DateTimeOffset EndsUtc,
    string Mode,
    IReadOnlyList<string> AffectedModules,
    string Title,
    string Message,
    bool IsActive,
    string? RowVersion);

public sealed record MobileIntegrationReferenceAdminDto(
    string Id,
    string? TenantKey,
    string Type,
    string Provider,
    string Environment,
    string SecretReference,
    bool IsEnabled,
    string HealthState,
    DateTimeOffset? LastVerifiedUtc,
    string? RowVersion);
