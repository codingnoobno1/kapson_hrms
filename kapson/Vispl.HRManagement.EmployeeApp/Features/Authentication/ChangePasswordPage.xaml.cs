using Vispl.HRManagement.Mobile.Client.Auth;

namespace Vispl.HRManagement.EmployeeApp.Features.Authentication;

/// <summary>
/// Shown after sign-in (or at launch) when the server says the password must be changed —
/// typically the first sign-in with a temporary password from HR. Success swaps in the fresh
/// session the server returns and opens the app; there is no way past this page without it.
/// </summary>
public partial class ChangePasswordPage : ContentPage
{
    private readonly MobileAuthenticator authenticator;

    public ChangePasswordPage(MobileAuthenticator authenticator)
    {
        InitializeComponent();
        this.authenticator = authenticator;
    }

    private void OnCurrentCompleted(object? sender, EventArgs e) => NewPasswordEntry.Focus();

    private void OnNewCompleted(object? sender, EventArgs e) => ConfirmPasswordEntry.Focus();

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        ErrorLabel.IsVisible = false;

        var current = CurrentPasswordEntry.Text ?? string.Empty;
        var next = NewPasswordEntry.Text ?? string.Empty;
        var confirm = ConfirmPasswordEntry.Text ?? string.Empty;

        // Checked here so an obvious slip costs no round trip. The server enforces the full
        // policy and its reasons are shown as they come back.
        var problem = Validate(current, next, confirm);
        if (problem is not null)
        {
            ShowError(problem);
            return;
        }

        SetBusy(true);

        try
        {
            var outcome = await authenticator.ChangePasswordAsync(current, next);

            if (!outcome.Succeeded)
            {
                ShowError(outcome.Message ?? "The password could not be changed.");
                return;
            }

            CurrentPasswordEntry.Text = NewPasswordEntry.Text = ConfirmPasswordEntry.Text = string.Empty;

            Application.Current!.Windows[0].Page = App.Services.GetRequiredService<AppShell>();
        }
        catch (Exception exception)
        {
            Vispl.HRManagement.EmployeeApp.Shared.Services.CrashGuard.Report(exception);
            ShowError("The password could not be changed. Try again.");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnSignOutClicked(object? sender, EventArgs e)
    {
        SetBusy(true);
        try
        {
            // Clearing the session raises SignedOut, and App returns the window to sign-in.
            await authenticator.SignOutAsync();
        }
        catch (Exception exception)
        {
            Vispl.HRManagement.EmployeeApp.Shared.Services.CrashGuard.Report(exception);
            SetBusy(false);
        }
    }

    private static string? Validate(string current, string next, string confirm)
    {
        if (current.Length == 0 || next.Length == 0 || confirm.Length == 0)
            return "Fill in all three fields.";
        if (!string.Equals(next, confirm, StringComparison.Ordinal))
            return "The new passwords do not match.";
        if (string.Equals(current, next, StringComparison.Ordinal))
            return "The new password must be different from the current one.";
        if (next.Length < 10 ||
            !next.Any(char.IsUpper) ||
            !next.Any(char.IsLower) ||
            !next.Any(char.IsDigit) ||
            next.All(char.IsLetterOrDigit))
            return "Use at least 10 characters, with an uppercase letter, a lowercase letter, a number and a symbol.";
        return null;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void SetBusy(bool busy)
    {
        Busy.IsVisible = busy;
        Busy.IsRunning = busy;
        SaveButton.IsEnabled = !busy;
        SignOutButton.IsEnabled = !busy;
        CurrentPasswordEntry.IsEnabled = !busy;
        NewPasswordEntry.IsEnabled = !busy;
        ConfirmPasswordEntry.IsEnabled = !busy;
    }
}
