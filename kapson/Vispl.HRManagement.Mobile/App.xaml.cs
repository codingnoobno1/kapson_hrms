using Microsoft.Extensions.DependencyInjection;
using Vispl.HRManagement.Mobile.Features.Authentication.Pages;
using Vispl.HRManagement.Mobile.Shared.Motion;
using Vispl.HRManagement.Mobile.Shared.Services;

namespace Vispl.HRManagement.Mobile;

public partial class App : Application
{
    private readonly IServiceProvider services;
    private readonly IAppNavigator navigator;

    public App(IServiceProvider services, IAppNavigator navigator)
    {
        InitializeComponent();
        this.services = services;
        this.navigator = navigator;

        // MotionSettings is static because XAML-constructed controls (KpiTile, JobCard, ...)
        // have no way to ask the container for a service; this is the one place that resolves
        // the real platform implementation and hands it over.
        MotionSettings.Service = services.GetService<IReducedMotionService>();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(services.GetRequiredService<LoginPage>())
        {
            Title = "HR Management",
            MinimumWidth = 390,
            MinimumHeight = 720
        };

        navigator.Attach(window);

        // A stored session is restored before the window settles, so somebody who signed in
        // yesterday lands on the dashboard rather than being asked to sign in every morning.
        // It starts on the login page because deciding needs a network round trip, and
        // flashing the workspace at somebody who is signed out is the worse way to be wrong.
        Dispatcher.Dispatch(async () => await RestoreSessionAsync());

        return window;
    }

    private async Task RestoreSessionAsync()
    {
        var authentication = services.GetRequiredService<IAuthenticationService>();

        if (await authentication.TryRestoreAsync())
        {
            await navigator.ShowWorkspaceAsync();
        }
    }
}
