using Vispl.HRManagement.Mobile.Features.Applicants.ViewModels;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Features.Applicants.Pages;

public partial class ApplicantsPage : ContentPage
{
    public ApplicantsPage(ApplicantsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = ViewModel = viewModel;
    }

    public ApplicantsViewModel ViewModel { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Reloaded on every appearance. Data is live now, so what was true when the tab
        // was last open is not necessarily true when it is returned to. The arrival fade/rise
        // runs alongside the load rather than waiting on it, so the page settles in immediately.
        await Task.WhenAll(RootLayout.PlayArrivalAsync(), ViewModel.LoadAsync());
    }
}
