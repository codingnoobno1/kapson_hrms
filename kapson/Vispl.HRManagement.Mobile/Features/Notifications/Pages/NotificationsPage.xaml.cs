using Vispl.HRManagement.Mobile.Features.Notifications.ViewModels;
using Vispl.HRManagement.Mobile.Shared.Motion;

namespace Vispl.HRManagement.Mobile.Features.Notifications.Pages;

public partial class NotificationsPage : ContentPage
{
    public NotificationsPage(NotificationsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = ViewModel = viewModel;
    }

    public NotificationsViewModel ViewModel { get; }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Reloaded on every appearance. Data is live now, so what was true when the tab
        // was last open is not necessarily true when it is returned to. The arrival fade/rise
        // runs alongside the load rather than waiting on it, so the page settles in immediately.
        await Task.WhenAll(RootLayout.PlayArrivalAsync(), ViewModel.LoadAsync());
    }

    // The notification row is an inline DataTemplate rather than an extracted control (unlike
    // JobCard/ApplicantCard/InterviewCard), so its arrival is hooked here instead of in a
    // control's own constructor.
    private async void OnNotificationCardLoaded(object? sender, EventArgs e)
    {
        if (sender is VisualElement element)
        {
            await element.PlayArrivalAsync(element.StaggerDelayFor());
        }
    }
}
