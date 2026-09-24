using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Workforce;
using Vispl.HRManagement.EmployeeApp.Features.Shared;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.EmployeeApp.Shared.ViewModels;

namespace Vispl.HRManagement.EmployeeApp.Features.Attendance;

/// <summary>
/// EMP-012 — 6.png, exactly: "‹ Attendance", four outlined stat tiles, Recent Attendance rows
/// with the chip under the text, View Full Month and Analytics. The whole page scrolls, tiles
/// included. There is no month stepper here; Full Month has one.
///
/// Every figure is the server's: tiles from <c>me/kpis/attendance</c>, rows from
/// <c>attendance/me</c> with the chip (<c>Badge</c>) and check-in / check-out already decided —
/// late is measured against the employee's shift, the same rule as the HR late-arrival report.
/// </summary>
public sealed class AttendancePage : ContentPage
{
    private readonly AttendanceViewModel model;
    private readonly BrandAppBar appBar = new();
    private readonly Grid tileGrid;
    private readonly VerticalStackLayout dayList = new() { Spacing = 0 };
    private readonly StateView stateView = new();
    private readonly VerticalStackLayout content;
    private readonly RefreshView refresher;
    private readonly AppSheet sheet = new();

    private readonly Presence.PresenceFlows presence;

    public AttendancePage(AttendanceViewModel model, QuickActionsHost quickActions, Presence.PresenceFlows presence)
    {
        this.model = model;
        this.presence = presence;
        presence.Changed += async (_, _) => await model.RefreshAsync();

        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        BackgroundColor = Tokens.Background;
        Title = "Attendance";

        tileGrid = new Grid
        {
            ColumnSpacing = Tokens.SpaceCardGap,
            RowSpacing = Tokens.SpaceCardGap,
            Margin = new Thickness(Tokens.SpaceGutter, 4, Tokens.SpaceGutter, 0),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)]
        };

        content = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                AttendanceVisuals.BackHeader("Attendance", () => Shell.Current.GoToAsync("//home")),
                tileGrid,
                AttendanceVisuals.SectionTitle("Recent Attendance"),
                dayList,
                Actions()
            }
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

        var tabBar = new EmployeeTabBar(EmployeeTab.Attendance);
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

        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ViewModelBase.State))
            {
                MainThread.BeginInvokeOnMainThread(Render);
            }
        };
        model.MonthChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Render);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        model.ResetToCurrentMonth();
        await model.LoadAsync();
    }

    private View Actions()
    {
        var grid = new Grid
        {
            ColumnSpacing = Tokens.SpaceCardGap,
            Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceSectionGap, Tokens.SpaceGutter, 24),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)]
        };

        grid.Add(AttendanceVisuals.IconButton(AttendanceVisuals.IconList, "View Full Month", Tokens.SecondaryButton, Tokens.TextPrimary,
            () => Shell.Current.GoToAsync("fullmonth")), 0);
        grid.Add(AttendanceVisuals.IconButton(AttendanceVisuals.IconChart, "Analytics", Tokens.BrandAccent, Colors.White,
            () => Shell.Current.GoToAsync("analytics")), 1);

        return grid;
    }

    private void Render()
    {
        content.IsVisible = model.IsReady || model.IsEmpty;
        RenderTiles();
        RenderDays();
    }

    private void RenderTiles()
    {
        tileGrid.Clear();

        for (var index = 0; index < model.Tiles.Count; index++)
        {
            tileGrid.Add(AttendanceVisuals.StatTile(model.Tiles[index]), index % 2, index / 2);
        }
    }

    private void RenderDays()
    {
        dayList.Clear();

        var days = model.RecentDays.Count > 0 ? model.RecentDays : model.Days;

        if (days.Count == 0)
        {
            dayList.Add(new Label
            {
                Text = "No attendance recorded in the last 30 days.",
                FontSize = 13,
                TextColor = Tokens.TextSecondary,
                Margin = new Thickness(Tokens.SpaceGutter, 4)
            });
        }

        foreach (var day in days.Take(15))
        {
            var row = AttendanceVisuals.RecentRow(day);
            var captured = day;
            var tap = new TapGestureRecognizer();
            tap.Tapped += (_, _) => ShowDay(captured);
            row.GestureRecognizers.Add(tap);
            dayList.Add(row);
        }
    }

    /// <summary>EMP-021. One day, as a sheet.</summary>
    private void ShowDay(AttendanceDayDto day)
    {
        var body = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(0, 4, 0, 24) };

        body.Add(SheetContent.FieldCard(
            ("Status", day.Badge is null ? day.Status.Name : $"{day.Status.Name} · {day.Badge.Name}"),
            ("Shift", day.ShiftCode),
            ("Check-in", day.CheckIn?.ToString("h:mm tt")),
            ("Check-out", day.CheckOut?.ToString("h:mm tt")),
            ("Worked", $"{day.Worked.TotalHours:0.#} hours"),
            ("Expected", $"{day.Expected.TotalHours:0.#} hours")));

        if (day.Exceptions.Count > 0)
        {
            body.Add(SheetContent.SectionHeader("Exceptions"));
            body.Add(SheetContent.FieldCard(
                day.Exceptions.Select((text, index) => ($"Exception {index + 1}", (string?)text)).ToArray()));
        }

        // EMP-023 from the day itself, pre-filled with what was recorded (server still enforces
        // HR's Allow corrections switch and the 30-day window).
        if (day.Actions.FirstOrDefault(action => action.Action == "request-correction") is { IsAllowed: true } &&
            day.Date >= DateOnly.FromDateTime(DateTime.Today.AddDays(-30)))
        {
            var request = Leave.LeaveFlows.PrimaryButton("Request correction", Tokens.BrandAccent);
            request.Clicked += async (_, _) => await presence.PresentCorrectionAsync(sheet, day);
            body.Add(request);
        }

        sheet.Present(day.Date.ToString("d MMMM yyyy"), body);
    }

    private static View Row(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }
}

/// <summary>
/// The Attendance and Attendance Analytics widgets (phase C plan §1.1–1.2): back header, outlined
/// stat tile, recent row with chip, icon button, section title, line icons.
/// </summary>
internal static class AttendanceVisuals
{
    // 24-unit line icons (Lucide geometry), stroked in the widget's colour.
    public const string IconCheckCircle = "M22 11.08V12a10 10 0 1 1-5.93-9.14 M22 4 12 14.01l-3-3";
    public const string IconXCircle = "M12 2a10 10 0 1 0 0 20a10 10 0 1 0 0-20 M15 9l-6 6 M9 9l6 6";
    public const string IconHourglass = "M5 22h14 M5 2h14 M17 22v-4.17a2 2 0 0 0-.59-1.42L12 12l-4.41 4.41A2 2 0 0 0 7 17.83V22 M7 2v4.17a2 2 0 0 0 .59 1.42L12 12l4.41-4.41A2 2 0 0 0 17 6.17V2";
    public const string IconCalendar = "M8 2v4 M16 2v4 M3 10h18 M5 4h14a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z";
    public const string IconList = "M8 6h13 M8 12h13 M8 18h13 M3 6h.01 M3 12h.01 M3 18h.01";
    public const string IconChart = "M3 3v18h18 M8 17V11 M13 17V7 M18 17v-4";
    public const string IconDownload = "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4 M7 10l5 5 5-5 M12 15V3";
    public const string IconMail = "M4 4h16a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V6a2 2 0 0 1 2-2z M22 6l-10 7L2 6";
    public const string IconChevronLeft = "M15 18l-6-6 6-6";

    public static View Glyph(string data, Color colour, double size, double thickness = 2) => new Microsoft.Maui.Controls.Shapes.Path
    {
        Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
        Stroke = colour,
        StrokeThickness = thickness,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        Aspect = Stretch.Uniform,
        WidthRequest = size,
        HeightRequest = size,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center
    };

    /// <summary>C01: "‹ Attendance".</summary>
    public static View BackHeader(string title, Func<Task> back)
    {
        var grid = new Grid
        {
            Padding = new Thickness(Tokens.SpaceGutter - 4, 12, Tokens.SpaceGutter, 10),
            ColumnSpacing = 6,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)]
        };

        var chevron = new ContentView
        {
            WidthRequest = 32,
            HeightRequest = 32,
            Content = Glyph(IconChevronLeft, Tokens.TextPrimary, 20, 2.2)
        };
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await back();
        chevron.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(chevron, "Back");

        grid.Add(chevron, 0);
        grid.Add(new Label
        {
            Text = title,
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Tokens.TextPrimary,
            VerticalTextAlignment = TextAlignment.Center
        }, 1);

        SemanticProperties.SetHeadingLevel(grid, SemanticHeadingLevel.Level1);
        return grid;
    }

    public static View SectionTitle(string text) => new Label
    {
        Text = text,
        FontSize = 15,
        FontAttributes = FontAttributes.Bold,
        TextColor = Tokens.TextPrimary,
        Margin = new Thickness(Tokens.SpaceGutter, 20, Tokens.SpaceGutter, 8)
    };

    /// <summary>C02: outlined tile — icon, coloured value, grey label.</summary>
    public static View StatTile(KpiDto kpi)
    {
        var (icon, colour) = kpi.Key switch
        {
            EmployeeKpiKeys.PresentDays => (IconCheckCircle, Tokens.AttendancePresent),
            EmployeeKpiKeys.AbsentDays => (IconXCircle, Tokens.AttendanceAbsent),
            EmployeeKpiKeys.HalfDays => (IconHourglass, Tokens.AttendanceHalfDay),
            EmployeeKpiKeys.Holidays => (IconCalendar, Tokens.AttendanceHoliday),
            _ => (IconCalendar, Tokens.TextSecondary)
        };

        var tile = new Border
        {
            BackgroundColor = Tokens.Surface,
            Stroke = Tokens.StatTileBorder,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Padding = new Thickness(8, 14),
            Content = new VerticalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    Glyph(icon, colour, 24),
                    new Label
                    {
                        Text = kpi.Value,
                        FontSize = 26,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = colour,
                        HorizontalTextAlignment = TextAlignment.Center
                    },
                    new Label
                    {
                        Text = kpi.Title,
                        FontSize = 12,
                        TextColor = Tokens.TextSecondary,
                        HorizontalTextAlignment = TextAlignment.Center
                    }
                }
            }
        };

        SemanticProperties.SetDescription(tile, $"{kpi.Title}: {kpi.Value}");
        return tile;
    }

    /// <summary>C04: status icon · date · detail · chip under the text · hairline.</summary>
    public static View RecentRow(AttendanceDayDto day)
    {
        var badge = day.Badge ?? new Contracts.Mobile.Shared.StatusDto(day.Status.Code, day.Status.Name, day.Status.Tone);
        var (chipGround, chipInk) = Chip(badge.Code);

        var (icon, iconColour) = badge.Code switch
        {
            "on-time" or "late" => (IconCheckCircle, Tokens.AttendancePresent),
            "half-day" => (IconHourglass, Tokens.AttendanceHalfDay),
            "absent" or "leave" => (IconXCircle, Tokens.AttendanceAbsent),
            "holiday" => (IconCalendar, Tokens.AttendanceHoliday),
            _ => (IconCalendar, Tokens.TextSecondary)
        };

        var detail = day.CheckIn is null && day.CheckOut is null
            ? badge.Code == "leave" ? $"{day.Status.Name} - Approved Leave" : day.Status.Name
            : $"{day.Status.Name} . Check-in: {Time(day.CheckIn)} . Check-out: {Time(day.CheckOut)}";

        var chip = new Border
        {
            BackgroundColor = chipGround,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 3 },
            Padding = new Thickness(8, 2),
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 4, 0, 0),
            Content = new Label { Text = badge.Name, FontSize = 11, TextColor = chipInk }
        };

        var grid = new Grid
        {
            Padding = new Thickness(Tokens.SpaceGutter, 10),
            ColumnSpacing = 8,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)]
        };

        var iconView = Glyph(icon, iconColour, 17);
        iconView.VerticalOptions = LayoutOptions.Start;
        iconView.Margin = new Thickness(0, 2, 0, 0);
        grid.Add(iconView, 0);

        grid.Add(new VerticalStackLayout
        {
            Spacing = 1,
            Children =
            {
                new Label { Text = day.Date.ToString("dd-MMM-yyyy"), FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary },
                new Label { Text = detail, FontSize = 12, TextColor = Tokens.TextSecondary },
                chip
            }
        }, 1);

        var row = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                grid,
                new BoxView { HeightRequest = 1, Color = Color.FromArgb("#E5E7EB"), Margin = new Thickness(Tokens.SpaceGutter, 0) }
            }
        };

        SemanticProperties.SetDescription(row, $"{day.Date:d MMMM yyyy}. {detail}. {badge.Name}");
        return row;
    }

    public static (Color Ground, Color Ink) Chip(string code) => code switch
    {
        "on-time" => (Tokens.AttChipOnTime, Colors.White),
        "late" or "absent" => (Tokens.AttChipLate, Colors.White),
        "half-day" => (Tokens.AttChipHalf, Colors.White),
        "leave" => (Tokens.AttChipLeave, Tokens.TextPrimary),
        "holiday" => (Tokens.AttChipHoliday, Colors.White),
        _ => (Tokens.SecondaryButton, Tokens.TextPrimary)
    };

    /// <summary>C05 / C11: an icon + label button, radius 4.</summary>
    public static View IconButton(string icon, string text, Color ground, Color ink, Func<Task> onTap)
    {
        var button = new Border
        {
            BackgroundColor = ground,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 4 },
            HeightRequest = 42,
            Content = new HorizontalStackLayout
            {
                Spacing = 8,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children =
                {
                    Glyph(icon, ink, 16),
                    new Label { Text = text, FontSize = 14, TextColor = ink, VerticalTextAlignment = TextAlignment.Center }
                }
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            button.Opacity = 0.6;
            try { await onTap(); }
            finally { button.Opacity = 1; }
        };
        button.GestureRecognizers.Add(tap);
        SemanticProperties.SetDescription(button, text);
        return button;
    }

    private static string Time(TimeOnly? time) => time?.ToString("h:mm tt") ?? "—";
}
