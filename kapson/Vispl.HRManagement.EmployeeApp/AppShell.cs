using Vispl.HRManagement.EmployeeApp.Features.Attendance;
using Vispl.HRManagement.EmployeeApp.Features.Home;
using Vispl.HRManagement.EmployeeApp.Features.Leave;
using Vispl.HRManagement.EmployeeApp.Features.Manage;
using Vispl.HRManagement.EmployeeApp.Features.Menu;
using Vispl.HRManagement.EmployeeApp.Features.Notifications;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;

namespace Vispl.HRManagement.EmployeeApp;

/// <summary>
/// Four tab roots and no flyout, in the order 1.png / 5.png draw them: Home · Leave · (+) ·
/// Attendance · Menu. The image's Projects slot is Leave (build plan D1).
///
/// The Shell still owns the tab routes, but its stock bar is hidden on every root: MAUI's bar
/// cannot carry the raised coral (+) that overlaps it, so each root draws
/// <c>EmployeeTabBar</c> itself and navigates with these routes.
///
/// This is deliberately not the HR app's role-aware Shell. There is one actor — the employee.
/// </summary>
public sealed class AppShell : Shell
{
    public AppShell()
    {
        FlyoutBehavior = FlyoutBehavior.Disabled;

        Shell.SetTabBarBackgroundColor(this, Tokens.Surface);
        Shell.SetTabBarForegroundColor(this, Tokens.BrandAccent);
        Shell.SetTabBarTitleColor(this, Tokens.BrandAccent);
        Shell.SetTabBarUnselectedColor(this, Tokens.TextSecondary);
        Shell.SetNavBarIsVisible(this, false);

        var tabs = new TabBar
        {
            Items =
            {
                Tab("Home", "home", typeof(HomePage)),
                Tab("Leave", "leave", typeof(LeavePage)),
                Tab("Attendance", "attendance", typeof(AttendancePage)),
                Tab("Menu", "menu", typeof(MenuPage)),
                // HR routes — the custom EmployeeTabBar navigates to these for HR users.
                Tab("Manage", "manage", typeof(ManagePage)),
                Tab("Interview Status", "interview-status", typeof(InterviewStatusPage))
            }
        };

        Items.Add(tabs);

        // Pushed pages, reachable from any tab. Registered by route rather than placed in the
        // bar, so opening one keeps the tab it was opened from underneath it.
        Routing.RegisterRoute("fullmonth", typeof(FullMonthPage));
        Routing.RegisterRoute("analytics", typeof(AttendanceAnalyticsPage));
        Routing.RegisterRoute("notifications", typeof(NotificationsPage));
        Routing.RegisterRoute("theme-settings", typeof(ThemeSettingsPage));
    }

    private static ShellContent Tab(string title, string route, Type page) => new()
    {
        Title = title,
        Route = route,
        ContentTemplate = new DataTemplate(page)
    };
}
