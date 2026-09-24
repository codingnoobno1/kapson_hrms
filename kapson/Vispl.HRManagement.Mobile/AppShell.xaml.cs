using Vispl.HRManagement.Mobile.Features.Applicants.Pages;
using Vispl.HRManagement.Mobile.Features.Home.Pages;
using Vispl.HRManagement.Mobile.Features.Interviews.Pages;
using Vispl.HRManagement.Mobile.Features.Jobs.Pages;
using Vispl.HRManagement.Mobile.Features.Notifications.Pages;
using Vispl.HRManagement.Mobile.Shared.Services;

namespace Vispl.HRManagement.Mobile;

public partial class AppShell : Shell
{
    private readonly IAuthenticationService authenticationService;
    private readonly IAppNavigator navigator;

    public AppShell(
        DashboardPage dashboardPage,
        JobsPage jobsPage,
        ApplicantsPage applicantsPage,
        InterviewsPage interviewsPage,
        NotificationsPage notificationsPage,
        IAuthenticationService authenticationService,
        IAppNavigator navigator)
    {
        InitializeComponent();
        this.authenticationService = authenticationService;
        this.navigator = navigator;

        var workspace = new FlyoutItem
        {
            Title = "Workspace",
            Route = "workspace",
            FlyoutDisplayOptions = FlyoutDisplayOptions.AsMultipleItems
        };
        workspace.Items.Add(CreateTab("Home", "⌂", "home", dashboardPage));
        workspace.Items.Add(CreateTab("Manage", "▤", "jobs", jobsPage));
        workspace.Items.Add(CreateTab("Applicants", "◎", "applicants", applicantsPage));
        workspace.Items.Add(CreateTab("Interviews", "↻", "interviews", interviewsPage));
        workspace.Items.Add(CreateTab("Alerts", "●", "notifications", notificationsPage));
        Items.Add(workspace);

        var signOut = new MenuItem { Text = "Sign out" };
        signOut.Clicked += SignOutClicked;
        Items.Add(signOut);
    }

    private static Tab CreateTab(string title, string glyph, string route, Page page)
    {
        var tab = new Tab
        {
            Title = title,
            Route = route,
            Icon = new FontImageSource { Glyph = glyph, Size = 20, Color = Color.FromArgb("#286BE6") }
        };
        tab.Items.Add(new ShellContent { Title = title, Route = $"{route}-content", Content = page });
        return tab;
    }

    private async void SignOutClicked(object? sender, EventArgs e)
    {
        await authenticationService.SignOutAsync();
        await navigator.ShowLoginAsync();
    }
}
