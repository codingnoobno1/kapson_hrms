using Vispl.HRManagement.MobileApp.Services;
using Vispl.HRManagement.MobileApp.Services.Clients;
using Vispl.HRManagement.MobileApp.Services.Ui;

namespace Vispl.HRManagement.MobileApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        var options = new MobileAppOptions();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new HttpClient { Timeout = options.Timeout });
        builder.Services.AddSingleton<ITokenStore, SecureTokenStore>();
        builder.Services.AddSingleton<AppSession>();
        builder.Services.AddSingleton<HrApiClient>();
        builder.Services.AddSingleton<HrRealtimeClient>();

        builder.Services.AddSingleton<AuthApi>();
        builder.Services.AddSingleton<DiscoveryApi>();
        builder.Services.AddSingleton<MastersApi>();
        builder.Services.AddSingleton<JobsApi>();
        builder.Services.AddSingleton<ApplicationsApi>();
        builder.Services.AddSingleton<InterviewsApi>();
        builder.Services.AddSingleton<HomeApi>();
        builder.Services.AddSingleton<PeopleApi>();
        builder.Services.AddSingleton<LeaveApi>();
        builder.Services.AddSingleton<AttendanceApi>();
        builder.Services.AddSingleton<FeatureApi>();
        builder.Services.AddSingleton<ScreenData>();
        builder.Services.AddSingleton<AppThemeService>();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
