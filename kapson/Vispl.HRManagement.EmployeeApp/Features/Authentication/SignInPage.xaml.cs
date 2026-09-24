using Vispl.HRManagement.Mobile.Client.Auth;

namespace Vispl.HRManagement.EmployeeApp.Features.Authentication;

/// <summary>
/// EMP-002. Username and password against the real token endpoint.
///
/// There is no preview or demo path here. The HR app shipped with a
/// <c>PreviewAuthenticationService</c> that accepted any email and a fixed password; this app
/// has nothing equivalent, so it cannot accidentally ship one.
/// </summary>
public partial class SignInPage : ContentPage
{
    private readonly MobileAuthenticator authenticator;

    public SignInPage(MobileAuthenticator authenticator)
    {
        InitializeComponent();
        this.authenticator = authenticator;
    }

    private void OnUserNameCompleted(object? sender, EventArgs e) => PasswordEntry.Focus();

    private async void OnSignInClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;
        SetBusy(true);

        try
        {
            var outcome = await authenticator.SignInAsync(
                UserNameEntry.Text ?? string.Empty,
                PasswordEntry.Text ?? string.Empty);

            if (!outcome.Succeeded)
            {
                ErrorLabel.Text = outcome.Message;
                ErrorLabel.IsVisible = true;
                return;
            }

            // The password is cleared before anything navigates: a page left on the stack
            // holding a credential in an Entry is a credential in memory for as long as the
            // page lives.
            PasswordEntry.Text = string.Empty;

            if (outcome.MustChangePassword)
            {
                // The server can demand a change before real work. There is no change-password
                // screen in the plan yet, so the person is told plainly rather than dropped
                // into an app that will refuse every call.
                await DisplayAlert(
                    "Password change required",
                    "Your account needs a new password before you can use the app. " +
                    "Please set one through HR, then sign in again.",
                    "OK");

                await authenticator.SignOutAsync();
                return;
            }

            Application.Current!.Windows[0].Page = App.Services.GetRequiredService<AppShell>();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        Busy.IsVisible = busy;
        Busy.IsRunning = busy;
        SignInButton.IsEnabled = !busy;
        UserNameEntry.IsEnabled = !busy;
        PasswordEntry.IsEnabled = !busy;
    }
}
