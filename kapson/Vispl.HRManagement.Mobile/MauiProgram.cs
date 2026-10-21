using Microsoft.Extensions.Logging;
using Vispl.HRManagement.Mobile.Features.Applicants.Pages;
using Vispl.HRManagement.Mobile.Features.Applicants.ViewModels;
using Vispl.HRManagement.Mobile.Features.Authentication.Pages;
using Vispl.HRManagement.Mobile.Features.Authentication.ViewModels;
using Vispl.HRManagement.Mobile.Features.Home.Pages;
using Vispl.HRManagement.Mobile.Features.Home.ViewModels;
using Vispl.HRManagement.Mobile.Features.Interviews.Pages;
using Vispl.HRManagement.Mobile.Features.Interviews.ViewModels;
using Vispl.HRManagement.Mobile.Features.Jobs.Pages;
using Vispl.HRManagement.Mobile.Features.Jobs.ViewModels;
using Vispl.HRManagement.Mobile.Features.Notifications.Pages;
using Vispl.HRManagement.Mobile.Features.Notifications.ViewModels;
using Vispl.HRManagement.Mobile.Client;
using Vispl.HRManagement.Mobile.Client.Auth;
using Vispl.HRManagement.Mobile.Shared.Motion;
using Vispl.HRManagement.Mobile.Shared.Services;

namespace Vispl.HRManagement.Mobile;

public static class MauiProgram
{
    /// <summary>
    /// Where this build talks to. Matches the Host's launchSettings: https on 7060, http on
    /// 7000. An Android emulator reaches the host machine at 10.0.2.2, never at localhost.
    /// </summary>
    private static string ResolveApiBaseAddress()
    {
        var saved = Preferences.Default.Get(
            "kapsons.api.base_url",
            "https://untutelary-francisco-overtrustfully.ngrok-free.dev");
        return string.IsNullOrWhiteSpace(saved)
            ? "https://untutelary-francisco-overtrustfully.ngrok-free.dev"
            : saved.Trim();
    }

    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(_ => { });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // The typed client layer, shared with the employee app. This replaced
        // DemoDataService and PreviewAuthenticationService, which between them meant the app
        // showed hardcoded records to somebody who had never actually authenticated.
        builder.Services.AddMobileApiClient(options =>
        {
            options.BaseAddress = ResolveApiBaseAddress();
            options.DeviceId = HrDeviceIdentity.Resolve();

            // This head is the HR portal's app. An employee signing in here would get a
            // recruiter's shell, so it is refused before the session starts.
            options.AllowedPortals = ["Hr"];
        });

        builder.Services.AddSingleton<ITokenStore, HrSecureTokenStore>();
        builder.Services.AddSingleton<IAuthenticationService, HrAuthenticationService>();
        builder.Services.AddSingleton<IHrDataService, HrDataService>();
        builder.Services.AddSingleton<IAppNavigator, AppNavigator>();

#if ANDROID
        builder.Services.AddSingleton<IReducedMotionService, Platforms.Android.ReducedMotionService>();
#elif WINDOWS
        builder.Services.AddSingleton<IReducedMotionService, Platforms.Windows.ReducedMotionService>();
#elif IOS
        builder.Services.AddSingleton<IReducedMotionService, Platforms.iOS.ReducedMotionService>();
#elif MACCATALYST
        builder.Services.AddSingleton<IReducedMotionService, Platforms.MacCatalyst.ReducedMotionService>();
#endif

        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<LoginViewModel>();
        builder.Services.AddSingleton<DashboardPage>();
        builder.Services.AddSingleton<DashboardViewModel>();
        builder.Services.AddSingleton<JobsPage>();
        builder.Services.AddSingleton<JobsViewModel>();
        builder.Services.AddTransient<JobDetailsPage>();
        builder.Services.AddTransient<JobDetailsViewModel>();
        builder.Services.AddSingleton<InterviewsPage>();
        builder.Services.AddSingleton<InterviewsViewModel>();
        builder.Services.AddSingleton<ApplicantsPage>();
        builder.Services.AddSingleton<ApplicantsViewModel>();
        builder.Services.AddTransient<ApplicantDetailsPage>();
        builder.Services.AddTransient<ApplicantDetailsViewModel>();
        builder.Services.AddSingleton<NotificationsPage>();
        builder.Services.AddSingleton<NotificationsViewModel>();

        return builder.Build();
    }
}
