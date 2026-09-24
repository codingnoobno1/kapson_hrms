using Vispl.HRManagement.Mobile.Features.Jobs.ViewModels;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Features.Jobs.Pages;

public partial class JobsPage : ContentPage
{
    public JobsPage(JobsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = ViewModel = viewModel;
    }

    public JobsViewModel ViewModel { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Reloaded on every appearance. Data is live now, so what was true when the tab
        // was last open is not necessarily true when it is returned to. The arrival fade/rise
        // runs alongside the load rather than waiting on it, so the page settles in immediately.
        await Task.WhenAll(RootLayout.PlayArrivalAsync(), ViewModel.LoadAsync());
    }
}
