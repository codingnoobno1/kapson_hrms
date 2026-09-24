using Vispl.HRManagement.Mobile.Features.Authentication.ViewModels;

namespace Vispl.HRManagement.Mobile.Features.Authentication.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
