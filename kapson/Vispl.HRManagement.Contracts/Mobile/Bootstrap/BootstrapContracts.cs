using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Mobile.Bootstrap;

public sealed class ResolveTenantRequest
{
    [Required, StringLength(240, MinimumLength = 2)]
    public string CompanyCodeOrDomain { get; set; } = string.Empty;
}

public sealed record TenantDiscoveryDto(
    string TenantPublicId,
    string TenantKey,
    string DisplayName,
    string? LogoUrl,
    string Authority,
    IReadOnlyList<string> SignInProviders,
    bool IsAvailable);

public sealed record PublicMobileMetadataDto(
    string ApiVersion,
    string MinimumAppVersion,
    IReadOnlyList<string> SupportedPlatforms,
    string Authority,
    string Audience,
    string StatusUrl,
    IReadOnlyDictionary<string, string> LegalLinks);

public sealed record SessionUserDto(
    string UserId,
    string? EmployeeId,
    string DisplayName,
    string? EmailMasked,
    string? AvatarUrl);

public sealed record SessionTenantDto(
    string TenantId,
    string TenantKey,
    string DisplayName,
    string? LogoUrl,
    string TimeZoneId,
    string Locale,
    string CurrencyCode);

public sealed record MobileSessionDto(
    SessionUserDto User,
    SessionTenantDto Tenant,
    string Portal,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<string> Scopes,
    IReadOnlyList<string> Modules,
    DateTimeOffset SessionExpiresUtc,
    bool MustChangePassword);

public sealed class LogoutRequest
{
    [Required, StringLength(200)]
    public string DeviceId { get; set; } = string.Empty;

    public bool RevokeAllDeviceTokens { get; set; }
}

public sealed class RegisterDeviceRequest
{
    [Required, StringLength(200)] public string InstallationId { get; set; } = string.Empty;
    [Required, StringLength(40)] public string Platform { get; set; } = string.Empty;
    [Required, StringLength(120)] public string Model { get; set; } = string.Empty;
    [Required, StringLength(80)] public string OperatingSystemVersion { get; set; } = string.Empty;
    [Required, StringLength(40)] public string AppVersion { get; set; } = string.Empty;
    [StringLength(4000)] public string? Attestation { get; set; }
}

public sealed record RegisteredDeviceDto(
    string Id,
    string Name,
    string Platform,
    string Model,
    string TrustState,
    bool IsCurrent,
    DateTimeOffset LastSeenUtc,
    bool PushEnabled,
    string? RowVersion);

public sealed class UpdatePushTokenRequest
{
    [Required, StringLength(40)] public string Provider { get; set; } = string.Empty;
    [Required, StringLength(4096)] public string Token { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Environment { get; set; } = string.Empty;
    [Required, StringLength(200)] public string AppBundleId { get; set; } = string.Empty;
}
