using Vispl.HRManagement.Mobile.Features.Jobs.ViewModels;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Features.Jobs.Pages;

public partial class JobDetailsPage : ContentPage
{
    private readonly JobDetailsViewModel viewModel;

    public JobDetailsPage(JobDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = this.viewModel = viewModel;
    }

    public void Load(long jobId) => viewModel.For(jobId);

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // This is a pushed page, not a true overlay sheet — approximates the spec's "bottom
        // sheet slides from bottom" with a larger rise than the other pages' plain arrival,
        // since there is no scrim/overlay to fade in behind a full-screen page.
        await Task.WhenAll(
            RootLayout.PlayArrivalAsync(riseDistance: 40, duration: MotionTokens.DurationEmphasis),
            viewModel.LoadAsync());
    }

    private async void BackClicked(object? sender, EventArgs e) => await Navigation.PopAsync();
}
