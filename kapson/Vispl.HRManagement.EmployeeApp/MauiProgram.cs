using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Core.Hosting;
using Syncfusion.Maui.Toolkit.Hosting;
using Vispl.HRManagement.EmployeeApp.Features.Attendance;
using Vispl.HRManagement.EmployeeApp.Features.Authentication;
using Vispl.HRManagement.EmployeeApp.Features.Home;
using Vispl.HRManagement.EmployeeApp.Features.Leave;
using Vispl.HRManagement.EmployeeApp.Features.Manage;
using Vispl.HRManagement.EmployeeApp.Features.Menu;
using Vispl.HRManagement.EmployeeApp.Features.Notifications;
using Vispl.HRManagement.EmployeeApp.Shared.Services;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.Mobile.Client;
using Vispl.HRManagement.Mobile.Client.Auth;

namespace Vispl.HRManagement.EmployeeApp;

public static class MauiProgram
{
    /// <summary>
    /// Where this build talks to.
    ///
    /// Hard-coded for now because there is no configuration story yet, and an app that silently
    /// points at the wrong host is worse than one that names its host in source. This moves to
    /// tenant discovery (EMP-003) before release.
    ///
    /// The ports match <c>Vispl.HRManagement.Host/Properties/launchSettings.json</c>: the https
    /// profile listens on 7060, the http profile on 7000.
    /// </summary>
    private static string ResolveApiBaseAddress()
    {
#if ANDROID
        // An emulator's "localhost" is the emulated device, not the machine hosting it. 10.0.2.2
        // is the loopback alias the Android emulator maps back to the host.
        //
        // Plain http, because the ASP.NET dev certificate is not trusted inside the emulator and
        // an https call fails with an opaque handshake error rather than anything diagnosable.
        // Release builds must use https against a real certificate.
        //
        // A physical device over USB has no 10.0.2.2 alias — instead run
        // `adb reverse tcp:7000 tcp:7000` so the device's own "localhost:7000" tunnels back to
        // the host machine through the USB debugging connection.
        return "http://localhost:7000";
#else
        return "https://localhost:7060";
#endif
    }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureSyncfusionCore()
            .ConfigureSyncfusionToolkit()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        RegisterSyncfusionLicense();

        // The typed client layer. Nothing above this line knows an HTTP verb, and nothing in
        // the app touches BM, DL, a repository or SQL Server.
        builder.Services.AddMobileApiClient(options =>
        {
            options.BaseAddress = ResolveApiBaseAddress();
            options.DeviceId = DeviceIdentity.Resolve();
        });

        // Replaces the library's in-memory fallback with the platform's protected store.
        builder.Services.AddSingleton<ITokenStore, SecureTokenStore>();

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddSingleton<EmployeeThemeService>();

        // The coral (+) sheet every tab root opens.
        builder.Services.AddTransient<Features.Shared.QuickActionsHost>();
        builder.Services.AddTransient<Features.Leave.LeaveFlows>();
        builder.Services.AddTransient<Features.Presence.PresenceFlows>();

        // Transient: a page holds loaded records, and a tab returned to should re-read rather
        // than show what was true when it was last open.
        builder.Services.AddTransient<SignInPage>();
        builder.Services.AddTransient<HomePage>();
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<AttendancePage>();
        builder.Services.AddTransient<FullMonthPage>();
        builder.Services.AddTransient<AttendanceAnalyticsPage>();
        builder.Services.AddTransient<AttendanceViewModel>();
        builder.Services.AddTransient<LeavePage>();
        builder.Services.AddTransient<LeaveViewModel>();
        builder.Services.AddTransient<MenuPage>();
        builder.Services.AddTransient<ThemeSettingsPage>();
        builder.Services.AddTransient<NotificationsPage>();
        builder.Services.AddTransient<NotificationsViewModel>();

        // HR/Recruitment: visible to users with RECRUITMENT.VIEW (replaces Leave/Attendance tabs).
        builder.Services.AddTransient<ManagePage>();
        builder.Services.AddTransient<InterviewStatusPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();

        App.Services = app.Services;
        return app;
    }

    /// <summary>
    /// Registers the Syncfusion licence.
    ///
    /// Read from the environment rather than committed: a licence key in source is a licence
    /// key in every clone and every build log. An unregistered build still runs and shows a
    /// trial banner, which is a visible failure rather than a silent one.
    /// </summary>
    private static void RegisterSyncfusionLicense()
    {
        var key = Environment.GetEnvironmentVariable("SYNCFUSION_LICENSE_KEY");

        if (!string.IsNullOrWhiteSpace(key))
        {
            Syncfusion.Licensing.SyncfusionLicenseProvider.RegisterLicense(key);
        }
        else
        {
            System.Diagnostics.Debug.WriteLine(
                "SYNCFUSION_LICENSE_KEY is not set. Syncfusion controls will show a trial banner.");
        }
    }
}
