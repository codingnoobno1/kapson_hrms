namespace Vispl.HRManagement.Api.Shared;

public sealed class MobileApiOptions
{
    public const string SectionName = "MobileApi";

    public string ApiVersion { get; set; } = "1.0";
    public string MinimumAppVersion { get; set; } = "0.1.0";
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = "vispl.hr.api";
    public bool RequireHttpsMetadata { get; set; } = true;
    public string StatusUrl { get; set; } = "/api/mobile/v1/discovery/metadata";
    public string[] SupportedPlatforms { get; set; } = [];
    public Dictionary<string, string> LegalLinks { get; set; } = [];
}

public static class MobileAuthenticationDefaults
{
    public const string BearerScheme = "MobileBearer";
    public const string PermissionPolicyPrefix = "MobilePermission:";
}
