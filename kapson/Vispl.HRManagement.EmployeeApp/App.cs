using Vispl.HRManagement.EmployeeApp.Features.Authentication;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.Mobile.Client.Auth;
using Vispl.HRManagement.Mobile.Client.Realtime;

namespace Vispl.HRManagement.EmployeeApp;

/// <summary>
/// EMP-001. Restores a session, then opens on Home or on sign-in.
///
/// The window starts on a splash rather than on either destination, because deciding which
/// needs a network round trip and flashing the sign-in page at somebody who is already signed
/// in reads as being logged out.
/// </summary>
public sealed class App : Application
{
    /// <summary>
    /// The container, for the two places that legitimately need it: the shell swapping itself
    /// for sign-in when a session ends, and sign-in swapping back. Pages take their
    /// dependencies through their constructors everywhere else.
    /// </summary>
    public static IServiceProvider Services { get; internal set; } = null!;

    private readonly MobileAuthenticator authenticator;
    private readonly MobileSession session;
    private readonly MobileRealtimeClient realtime;

    public App(MobileAuthenticator authenticator, MobileSession session, MobileRealtimeClient realtime, EmployeeThemeService themes)
    {
        this.authenticator = authenticator;
        this.session = session;
        this.realtime = realtime;

        // A session that ends mid-use returns to sign-in wherever the person happened to be,
        // rather than leaving them on a screen whose every call now fails.
        session.SignedOut += OnSignedOut;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(BuildSplash()) { Title = "Kapsons" };

        Dispatcher.Dispatch(async () => await StartAsync(window));

        return window;
    }

    private async Task StartAsync(Window window)
    {
        var restored = await authenticator.TryRestoreAsync();

        if (restored)
        {
            await realtime.StartAsync();
        }

        window.Page = restored
            ? Services.GetRequiredService<AppShell>()
            : Services.GetRequiredService<SignInPage>();
    }

    private void OnSignedOut(object? sender, EventArgs e) =>
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _ = realtime.StopAsync();
            if (Windows.Count > 0)
            {
                Windows[0].Page = Services.GetRequiredService<SignInPage>();
            }
        });

    private static Page BuildSplash() => new ContentPage
    {
        BackgroundColor = Tokens.Background,
        Content = new VerticalStackLayout
        {
            Spacing = 14,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "KAPSONS",
                    FontSize = 28,
                    FontAttributes = FontAttributes.Bold,
                    CharacterSpacing = 3,
                    TextColor = Tokens.TextPrimary,
                    HorizontalTextAlignment = TextAlignment.Center
                },
                new ActivityIndicator
                {
                    IsRunning = true,
                    Color = Tokens.BrandAccent,
                    HorizontalOptions = LayoutOptions.Center
                }
            }
        }
    };
}
