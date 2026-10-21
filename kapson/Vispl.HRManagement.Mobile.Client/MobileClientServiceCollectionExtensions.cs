using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Client.Auth;
using Vispl.HRManagement.Mobile.Client.Http;
using Vispl.HRManagement.Mobile.Client.Realtime;

namespace Vispl.HRManagement.Mobile.Client;

/// <summary>
/// Registers the typed clients an app head talks to the mobile API through.
///
/// The head supplies two things this library cannot know: where the server is, and how to store
/// a token securely on the platform it is running on.
/// </summary>
public static class MobileClientServiceCollectionExtensions
{
    public static IServiceCollection AddMobileApiClient(
        this IServiceCollection services,
        Action<MobileApiOptions> configure)
    {
        var options = new MobileApiOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.BaseAddress))
        {
            throw new InvalidOperationException(
                "MobileApiOptions.BaseAddress is required; the app cannot guess where the server is.");
        }

        services.AddSingleton(options);
        services.AddSingleton<MobileSession>();

        // A token store must be registered by the head. This is the fallback so the app runs
        // rather than failing to start — it costs a sign-in per launch, which is the safe way
        // to be wrong.
        services.AddSingleton<ITokenStore, InMemoryTokenStore>();

        services.AddHttpClient("kapsons", client =>
        {
            client.BaseAddress = new Uri(options.BaseAddress.TrimEnd('/') + "/");
            client.Timeout = options.Timeout;
            client.DefaultRequestHeaders.Add("X-Client", "kapsons-employee-app");
            client.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
        });

        services.AddSingleton(provider => new MobileApiClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("kapsons"),
            provider.GetRequiredService<MobileSession>(),
            provider.GetRequiredService<ITokenStore>(),
            provider.GetRequiredService<MobileApiOptions>()));

        services.AddSingleton(provider => new MobileAuthenticator(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("kapsons"),
            provider.GetRequiredService<MobileSession>(),
            provider.GetRequiredService<ITokenStore>(),
            provider.GetRequiredService<MobileApiOptions>()));

        services.AddSingleton<MobileRealtimeClient>();

        services.AddSingleton<MeApiClient>();
        services.AddSingleton<AttendanceApiClient>();
        services.AddSingleton<LeaveApiClient>();
        services.AddSingleton<EmployeeApiClient>();
        services.AddSingleton<PassesApiClient>();
        services.AddSingleton<OnDutyApiClient>();
        services.AddSingleton<NotificationsApiClient>();

        // Recruitment: the HR app's surface. Registered here rather than in a second extension
        // because both heads share one client library, and an unused client costs nothing.
        services.AddSingleton<RecruitmentApiClient>();
        services.AddSingleton<InterviewsApiClient>();
        services.AddSingleton<HrHomeApiClient>();

        return services;
    }
}
