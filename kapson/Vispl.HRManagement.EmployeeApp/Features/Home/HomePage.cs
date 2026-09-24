using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.EmployeeApp.Features.Shared;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.EmployeeApp.Shared.ViewModels;
using Vispl.HRManagement.Mobile.Client.Auth;
using Vispl.HRManagement.Mobile.Client.Realtime;

namespace Vispl.HRManagement.EmployeeApp.Features.Home;

/// <summary>
/// EMP-010. Home exactly as 5.png lays it out: app bar, coral identity card with the QR, the 2×2
/// pastel tiles, the Interview Schedule card for interviewers, and the bottom bar with the raised
/// coral (+).
/// </summary>
public sealed class HomePage : ContentPage
{
    private readonly HomeViewModel model;
    private readonly QuickActionsHost quickActions;
    private readonly BrandAppBar appBar = new();
    private readonly IdentityCard identityCard = new();
    private readonly InterviewScheduleCard interviewCard = new();
    private readonly Grid tileGrid;
    private readonly StateView stateView = new();
    private readonly RefreshView refresher;
    private readonly VerticalStackLayout content;
    private readonly AppSheet sheet = new();

    private readonly Mobile.Client.Api.MeApiClient me;
    private readonly MobileRealtimeClient realtime;
    private bool listening;
    private View? salaryTile;

    public HomePage(
        HomeViewModel model,
        QuickActionsHost quickActions,
        Mobile.Client.Api.MeApiClient me,
        MobileSession session,
        MobileRealtimeClient realtime)
    {
        this.model = model;
        this.quickActions = quickActions;
        this.me = me;
        this.realtime = realtime;

        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        BackgroundColor = Tokens.Background;
        Title = "Home";

        tileGrid = new Grid
        {
            ColumnSpacing = Tokens.SpaceCardGap,
            RowSpacing = Tokens.SpaceCardGap,
            Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceGutter, Tokens.SpaceGutter, 0),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)]
        };

        identityCard.Margin = new Thickness(Tokens.SpaceGutter, 12, Tokens.SpaceGutter, 0);
        interviewCard.Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceGutter, Tokens.SpaceGutter, 20);

        content = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { identityCard, tileGrid, interviewCard, HrPayrollSection(), WorkAndProjectsSection(), SupportSection() }
        };

        stateView.ViewModel = model;
        stateView.RetryRequested += async (_, _) => await model.LoadAsync();

        refresher = new RefreshView
        {
            RefreshColor = Tokens.BrandAccent,
            Content = new ScrollView { Content = new Grid { Children = { content, stateView } } }
        };

        refresher.Refreshing += async (_, _) =>
        {
            await model.RefreshAsync();
            refresher.IsRefreshing = false;
        };

        var tabBar = new EmployeeTabBar(EmployeeTab.Home, session);
        tabBar.QuickActionsRequested += (_, _) => quickActions.Present(sheet);

        sheet.Content = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            ],
            Children = { Row(appBar, 0), Row(refresher, 1), Row(tabBar, 2) }
        };

        Content = sheet;

        model.PropertyChanged += OnModelChanged;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!listening)
        {
            realtime.DataChanged += OnLiveDataChanged;
            listening = true;
        }
        await realtime.StartAsync();
        await Motion.EnterAsync(content);

        // Reloaded on every appearance rather than once: the tiles and the badge are stale the
        // moment the person punches, applies or reads a notification elsewhere.
        await model.LoadAsync();

        // Salary Slip follows HR's "Show Salary Details" switch; the server refuses it anyway.
        try
        {
            var features = await me.GetAppFeaturesAsync();
            if (salaryTile is not null)
            {
                salaryTile.IsVisible = features.ShowSalaryDetails;
            }
        }
        catch (Mobile.Client.Http.MobileApiException)
        {
        }
    }

    protected override void OnDisappearing()
    {
        if (listening)
        {
            realtime.DataChanged -= OnLiveDataChanged;
            listening = false;
        }
        base.OnDisappearing();
    }

    private void OnLiveDataChanged(object? sender, MobileDataChanged change)
    {
        if (change.Topic is not ("attendance" or "leave" or "notifications" or "onboarding" or "workspace")) return;
        MainThread.BeginInvokeOnMainThread(async () => await model.RefreshAsync());
    }

    /// <summary>C12–C13, the scrolled Home: "HR &amp; PAYROLL" with Salary Slip and Attendance.</summary>
    private View HrPayrollSection()
    {
        const string salaryIcon = "M4 2h16v20l-3-2-3 2-3-2-3 2-4-2V2z M12 6v12 M15 8.5c0-1.1-1.3-2-3-2s-3 .9-3 2 1.3 2 3 2 3 .9 3 2-1.3 2-3 2-3-.9-3-2";
        const string attendanceIcon = "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2 M9 3a4 4 0 1 0 0 8a4 4 0 1 0 0-8 M16 11l2 2 4-4";

        salaryTile = ServiceTile(salaryIcon, "Salary Slip", "View your monthly salary details", ShowSalaryAsync);
        var attendanceTile = ServiceTile(attendanceIcon, "Attendance", "Check your attendance records",
            () => Shell.Current.GoToAsync("//attendance"));

        return SectionOf("HR & PAYROLL", salaryTile, attendanceTile);
    }

    /// <summary>
    /// "My Projects" and "Tasks" have no backend module behind them (phase C decision Q3) — the
    /// tiles stay on Home because the mockup places them there, and each one opens the same
    /// not-ready panel every other unbuilt feature in this app uses. Nothing here is faked.
    /// </summary>
    private View WorkAndProjectsSection()
    {
        const string projectsIcon = "M3 7h18M3 7v12a2 2 0 002 2h14a2 2 0 002-2V7M8 7V5a2 2 0 012-2h4a2 2 0 012 2v2";
        const string tasksIcon = "M9 11l3 3L22 4 M21 12v7a2 2 0 01-2 2H5a2 2 0 01-2-2V5a2 2 0 012-2h11";

        var projectsTile = ServiceTile(projectsIcon, "My Projects", "View assigned project and tasks",
            () => ShowNotReadyAsync("My Projects", "Projects"));
        var tasksTile = ServiceTile(tasksIcon, "Tasks", "Manage your daily tasks and deadlines",
            () => ShowNotReadyAsync("Tasks", "Tasks"));

        return SectionOf("WORK & PROJECTS", projectsTile, tasksTile);
    }

    /// <summary>Help &amp; Company Information are static content — no server round trip, no data to fake.</summary>
    private View SupportSection()
    {
        const string helpIcon = "M12 22a10 10 0 100-20 10 10 0 000 20z M9.09 9a3 3 0 015.83 1c0 2-3 2-3 4 M12 17h.01";
        const string infoIcon = "M12 22a10 10 0 100-20 10 10 0 000 20z M12 16v-4 M12 8h.01";

        var helpTile = ServiceTile(helpIcon, "Help & Support", "Get help from HR team", ShowHelpAsync);
        var infoTile = ServiceTile(infoIcon, "Company Information", "Learn about Kapsons", ShowCompanyInfoAsync);

        return SectionOf("SUPPORT", helpTile, infoTile);
    }

    private static View SectionOf(string title, View tileA, View tileB)
    {
        var grid = new Grid
        {
            ColumnSpacing = Tokens.SpaceCardGap,
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 24),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)]
        };

        grid.Add(tileA, 0);
        grid.Add(tileB, 1);

        return new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = 12,
                    FontAttributes = FontAttributes.Bold,
                    CharacterSpacing = 0.5,
                    TextColor = Tokens.TextSecondary,
                    Margin = new Thickness(Tokens.SpaceGutter, 4, Tokens.SpaceGutter, 8)
                },
                grid
            }
        };
    }

    private Task ShowNotReadyAsync(string title, string module)
    {
        sheet.Present(title, SheetContent.NotReady(module));
        return Task.CompletedTask;
    }

    private Task ShowHelpAsync()
    {
        sheet.Present("Help & Support", SheetContent.FieldCard(
            ("Need a hand?", "Reach out to HR through the Kapsons helpdesk."),
            ("Email", "hr-support@kapsons.com"),
            ("Phone", "Available on your employee ID card")));
        return Task.CompletedTask;
    }

    private Task ShowCompanyInfoAsync()
    {
        sheet.Present("Company Information", SheetContent.FieldCard(
            ("Kapsons", "Door to global fashion."),
            ("About", "A retail fashion house operating stores across the region."),
            ("HR policies", "Ask HR for the current employee handbook.")));
        return Task.CompletedTask;
    }

    private static View ServiceTile(string icon, string title, string subtitle, Func<Task> onTap)
    {
        var iconView = Attendance.AttendanceVisuals.Glyph(icon, Tokens.TextSecondary, 22, 1.6);
        iconView.HorizontalOptions = LayoutOptions.Start;

        var tile = new Border
        {
            BackgroundColor = Tokens.WidgetCardTint,
            Stroke = Tokens.StatTileBorder,
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 10 },
            Padding = new Thickness(14, 12),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                Children =
                {
                    iconView,
                    new Label { Text = title, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary, Margin = new Thickness(0, 4, 0, 0) },
                    new Label { Text = subtitle, FontSize = 12, TextColor = Tokens.TextSecondary }
                }
            }
        };

        Motion.AddDepth(tile);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => { await Motion.PulseAsync(tile); await onTap(); };
        tile.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(tile, $"{title}. {subtitle}");
        return tile;
    }

    private async Task ShowSalaryAsync()
    {
        sheet.Present("Salary Details", new ActivityIndicator { IsRunning = true, Color = Tokens.BrandAccent, Margin = 32 });

        try
        {
            sheet.Present("Salary Details", Menu.SelfServiceSheets.Salary(await me.GetMySalaryAsync()));
        }
        catch (Mobile.Client.Http.MobileApiException exception)
        {
            sheet.Present("Salary Details", SheetContent.FieldCard(("Salary", exception.Message)));
        }
    }

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            switch (e.PropertyName)
            {
                case nameof(HomeViewModel.Identity):
                    identityCard.Identity = model.Identity;
                    break;

                case nameof(HomeViewModel.UnreadCount):
                    appBar.UnreadCount = model.UnreadCount;
                    break;

                case nameof(HomeViewModel.Interviews):
                    interviewCard.Bind(model.Interviews);
                    break;

                case nameof(ViewModelBase.State):
                    content.IsVisible = model.IsReady;
                    RenderTiles();
                    break;
            }
        });
    }

    private void RenderTiles()
    {
        tileGrid.Clear();
        tileGrid.RowDefinitions.Clear();

        for (var row = 0; row < Math.Max((model.Tiles.Count + 1) / 2, 1); row++)
        {
            tileGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (var index = 0; index < model.Tiles.Count; index++)
        {
            var kpi = model.Tiles[index];
            var tile = new KpiTile { Slot = index, Kpi = kpi };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                await Motion.PulseAsync(tile);
                await Shell.Current.GoToAsync(RouteFor(kpi.Key));
            };
            tile.GestureRecognizers.Add(tap);

            tileGrid.Add(tile, index % 2, index / 2);
        }
    }

    /// <summary>Where each tile leads. Bound by key, so a reordered grid still goes to the right place.</summary>
    private static string RouteFor(string key) => key switch
    {
        EmployeeKpiKeys.PresentDays => "//attendance",
        EmployeeKpiKeys.LeaveBalance or EmployeeKpiKeys.PendingRequests => "//leave",
        EmployeeKpiKeys.GatePasses => "//menu",
        _ => "//home"
    };

    private static View Row(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }
}
