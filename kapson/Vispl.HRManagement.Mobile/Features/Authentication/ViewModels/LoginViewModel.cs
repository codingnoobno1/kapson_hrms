using System.Windows.Input;
using Vispl.HRManagement.Mobile.Shared.Services;
using Vispl.HRManagement.Mobile.Shared.ViewModels;

namespace Vispl.HRManagement.Mobile.Features.Authentication.ViewModels;

public sealed class LoginViewModel : ScreenViewModel
{
    private readonly IAuthenticationService authenticationService;
    private readonly IAppNavigator navigator;

    // Blank, not pre-filled. The preview build seeded a demo address here, which is exactly the
    // sort of thing that survives into a release nobody meant to ship it in.
    private string userName = string.Empty;
    private string password = string.Empty;
    private bool rememberMe = true;
    private bool isPasswordHidden = true;

    public LoginViewModel(IAuthenticationService authenticationService, IAppNavigator navigator)
    {
        this.authenticationService = authenticationService;
        this.navigator = navigator;
        SignInCommand = new Command(async () => await SignInAsync(), () => !IsBusy);
        TogglePasswordCommand = new Command(() => IsPasswordHidden = !IsPasswordHidden);
    }

    /// <summary>
    /// A username, not an email. HR accounts sign in by username; the address on the account is
    /// where notices go, not what it authenticates with.
    /// </summary>
    public string UserName { get => userName; set => SetProperty(ref userName, value); }

    public string Password { get => password; set => SetProperty(ref password, value); }
    public bool RememberMe { get => rememberMe; set => SetProperty(ref rememberMe, value); }
    public bool IsPasswordHidden { get => isPasswordHidden; set => SetProperty(ref isPasswordHidden, value); }

    public ICommand SignInCommand { get; }
    public ICommand TogglePasswordCommand { get; }

    private async Task SignInAsync()
    {
        ErrorMessage = null;

        if (string.IsNullOrWhiteSpace(UserName) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Enter your username and password.";
            return;
        }

        IsBusy = true;
        ((Command)SignInCommand).ChangeCanExecute();

        try
        {
            // The server's own words reach the person: "not given mobile access" is a different
            // problem from a wrong password, and only one of them is worth retyping.
            var failure = await authenticationService.SignInAsync(UserName.Trim(), Password);

            if (failure is not null)
            {
                ErrorMessage = failure;
                return;
            }

            // Cleared before navigating: a page left on the stack holding a credential in an
            // Entry is a credential in memory for as long as that page lives.
            Password = string.Empty;

            await navigator.ShowWorkspaceAsync();
        }
        finally
        {
            IsBusy = false;
            ((Command)SignInCommand).ChangeCanExecute();
        }
    }
}
