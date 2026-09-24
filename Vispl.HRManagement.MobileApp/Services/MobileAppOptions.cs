namespace Vispl.HRManagement.MobileApp.Services;

public sealed class MobileAppOptions
{
    // The Android emulator reaches the developer machine through 10.0.2.2, never through localhost.
    public const string DefaultAndroidBaseAddress = "http://10.0.2.2:7000/";
    public const string DefaultBaseAddress = "http://localhost:7000/";

    public string BaseAddress { get; set; } = DefaultBaseAddress;

    public string AppVersion { get; set; } = AppInfo.VersionString;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
}
