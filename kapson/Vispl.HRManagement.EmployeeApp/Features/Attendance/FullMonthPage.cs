using Syncfusion.Maui.Calendar;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.EmployeeApp.Shared.ViewModels;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Client.Http;

namespace Vispl.HRManagement.EmployeeApp.Features.Attendance;

/// <summary>
/// EMP-020. The month grid, on Syncfusion's <see cref="SfCalendar"/>.
///
/// The calendar earns the dependency here: a month grid with leading and trailing days, week
/// starts, swipe navigation and per-cell templating is a lot of fiddly layout to hand-roll, and
/// none of it is this product's problem. What stays ours is the meaning — the cell colour comes
/// from the day's server-assigned status, and the legend beside it is mandatory, because colour
/// must never be the only thing carrying a state.
/// </summary>
public sealed class FullMonthPage : ContentPage
{
    private readonly AttendanceViewModel model;
    private readonly MeApiClient me;
    private readonly SfCalendar calendar;
    private readonly Grid tileGrid;
    private readonly StateView stateView = new();
    private readonly VerticalStackLayout content;
    private readonly AppSheet sheet = new();
    private readonly Label monthLabel;

    public FullMonthPage(AttendanceViewModel model, MeApiClient me)
    {
        this.model = model;
        this.me = me;

        Title = "Full Month Attendance";
        BackgroundColor = Tokens.Background;

        monthLabel = new Label
        {
            FontSize = Tokens.FontRowTitle,
            FontAttributes = FontAttributes.Bold,
            TextColor = Tokens.TextPrimary,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        tileGrid = new Grid
        {
            ColumnSpacing = Tokens.SpaceCardGap,
            RowSpacing = Tokens.SpaceCardGap,
            Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceCardGap, Tokens.SpaceGutter, 0),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)],
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)]
        };

        calendar = BuildCalendar();

        content = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { Stepper(), tileGrid, CalendarCard(), Legend(), ReportActions() }
        };

        stateView.ViewModel = model;
        stateView.RetryRequested += async (_, _) => await model.LoadAsync();

        sheet.Content = new ScrollView { Content = new Grid { Children = { content, stateView } } };
        Content = sheet;

        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ViewModelBase.State) or nameof(AttendanceViewModel.MonthLabel))
            {
                MainThread.BeginInvokeOnMainThread(Render);
            }
        };

        model.MonthChanged += (_, _) => MainThread.BeginInvokeOnMainThread(Render);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await model.LoadAsync();
    }

    private SfCalendar BuildCalendar()
    {
        var control = new SfCalendar
        {
            View = CalendarView.Month,
            SelectionMode = CalendarSelectionMode.Single,
            CanToggleDaySelection = true,
            ShowTrailingAndLeadingDates = false,
            HeightRequest = 330,
            Background = new SolidColorBrush(Tokens.CalendarGround),
            EnablePastDates = true,
            HeaderView = new CalendarHeaderView { Height = 0 },
            MonthView = new CalendarMonthView
            {
                FirstDayOfWeek = DayOfWeek.Sunday,
                CellTemplate = new DataTemplate(BuildCell)
            }
        };

        control.SelectionChanged += OnDateSelected;
        return control;
    }

    /// <summary>
    /// One cell. The fill is the day's status colour and the number sits on top of it; a date
    /// with no attendance row keeps the neutral future colour rather than borrowing a meaning.
    /// </summary>
    private object BuildCell()
    {
        var label = new Label
        {
            FontSize = 12,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var plate = new Border
        {
            Margin = 2,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCalendarCell },
            Content = label
        };

        plate.BindingContextChanged += (sender, _) =>
        {
            if (sender is not Border border || border.BindingContext is not CalendarCellDetails details)
            {
                return;
            }

            var day = model.DayFor(details.Date);

            border.BackgroundColor = day is null
                ? Tokens.CalFuture
                : Tokens.ForDayStatus(day.Status.Code);

            label.Text = details.Date.Day.ToString();
            label.TextColor = day is null ? Tokens.TextSecondary : Tokens.TextPrimary;

            SemanticProperties.SetDescription(
                border,
                day is null
                    ? $"{details.Date:d MMMM}, no record"
                    : $"{details.Date:d MMMM}, {day.Status.Name}");
        };

        return plate;
    }

    private void OnDateSelected(object? sender, CalendarSelectionChangedEventArgs e)
    {
        if (e.NewValue is not DateTime selected)
        {
            return;
        }

        var day = model.DayFor(selected);

        if (day is null)
        {
            sheet.Present(
                selected.ToString("d MMMM yyyy"),
                SheetContent.FieldCard(("Status", "No attendance recorded for this day.")));

            return;
        }

        sheet.Present(
            day.Date.ToString("d MMMM yyyy"),
            SheetContent.FieldCard(
                ("Status", day.Status.Name),
                ("Shift", day.ShiftCode),
                ("Worked", $"{day.Worked.TotalHours:0.#} hours"),
                ("Expected", $"{day.Expected.TotalHours:0.#} hours")));
    }

    private View Stepper()
    {
        var back = Step("‹", async () => await model.PreviousMonthAsync(), "Previous month");
        var forward = Step("›", async () => await model.NextMonthAsync(), "Next month");
        forwardButton = forward;

        var grid = new Grid
        {
            Margin = new Thickness(Tokens.SpaceGutter, 12, Tokens.SpaceGutter, 0),
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };

        grid.Add(back, 0);
        grid.Add(monthLabel, 1);
        grid.Add(forward, 2);
        return grid;
    }

    private View? forwardButton;

    private static View Step(string glyph, Func<Task> onTap, string description)
    {
        var border = new Border
        {
            WidthRequest = Tokens.MinimumTouchTarget,
            HeightRequest = Tokens.MinimumTouchTarget,
            BackgroundColor = Tokens.SurfaceMuted,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new Label
            {
                Text = glyph,
                FontSize = 18,
                TextColor = Tokens.TextPrimary,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await onTap();
        border.GestureRecognizers.Add(tap);

        SemanticProperties.SetDescription(border, description);
        return border;
    }

    private View CalendarCard() => new Border
    {
        Stroke = Tokens.Border,
        StrokeThickness = 1,
        BackgroundColor = Tokens.CalendarGround,
        Padding = 6,
        Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceSectionGap, Tokens.SpaceGutter, 0),
        StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
        Content = calendar
    };

    /// <summary>
    /// EW-055. Not optional. Colour alone never carries meaning, so every cell state is named
    /// here beside its swatch.
    /// </summary>
    private static View Legend()
    {
        var flex = new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceCardGap, Tokens.SpaceGutter, 0)
        };

        foreach (var (colour, name) in new[]
                 {
                     (Tokens.CalPresent, "Present"),
                     (Tokens.CalAbsent, "Absent"),
                     (Tokens.CalHalfDay, "Half day"),
                     (Tokens.CalHoliday, "Holiday"),
                     (Tokens.CalLeave, "Leave")
                 })
        {
            flex.Add(new HorizontalStackLayout
            {
                Spacing = 5,
                Margin = new Thickness(0, 0, 14, 6),
                Children =
                {
                    new Border
                    {
                        WidthRequest = 11,
                        HeightRequest = 11,
                        BackgroundColor = colour,
                        StrokeThickness = 0,
                        VerticalOptions = LayoutOptions.Center,
                        StrokeShape = new RoundRectangle { CornerRadius = 3 }
                    },
                    new Label { Text = name, FontSize = 11, TextColor = Tokens.TextSecondary }
                }
            });
        }

        return flex;
    }

    private View ReportActions()
    {
        var download = new Button
        {
            Text = "Download report",
            BackgroundColor = Tokens.SurfaceMuted,
            TextColor = Tokens.TextPrimary,
            CornerRadius = 12,
            HeightRequest = Tokens.MinimumTouchTarget
        };

        var email = new Button
        {
            Text = "Email report",
            BackgroundColor = Tokens.BrandAccent,
            TextColor = Tokens.OnAccent,
            CornerRadius = 12,
            HeightRequest = Tokens.MinimumTouchTarget
        };

        download.Clicked += async (_, _) => await DownloadReportAsync();
        email.Clicked += async (_, _) => await EmailReportAsync();

        var grid = new Grid
        {
            ColumnSpacing = Tokens.SpaceCardGap,
            Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceSectionGap, Tokens.SpaceGutter, 24),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)]
        };

        grid.Add(download, 0);
        grid.Add(email, 1);
        return grid;
    }

    private async Task DownloadReportAsync()
    {
        try
        {
            var report = await me.GetAttendanceReportForMonthAsync(model.Year, model.Month);
            var path = System.IO.Path.Combine(FileSystem.CacheDirectory, report.FileName);
            await File.WriteAllBytesAsync(path, Convert.FromBase64String(report.Base64Content));

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Attendance report",
                File = new ShareFile(path, report.ContentType)
            });
        }
        catch (MobileApiException exception)
        {
            await DisplayAlert("Download failed", exception.Message, "OK");
        }
    }

    private async Task EmailReportAsync()
    {
        try
        {
            var result = await me.EmailAttendanceReportForMonthAsync(model.Year, model.Month);
            await DisplayAlert(
                result.Sent ? "Report sent" : "Not sent",
                result.Sent ? $"Sent to {result.SentTo}." : result.Failure ?? "The email could not be sent.",
                "OK");
        }
        catch (MobileApiException exception)
        {
            await DisplayAlert("Email failed", exception.Message, "OK");
        }
    }

    private void Render()
    {
        monthLabel.Text = model.MonthLabel;
        content.IsVisible = model.IsReady || model.IsEmpty;

        if (forwardButton is not null)
        {
            forwardButton.Opacity = model.CanGoForward ? 1 : 0.35;
            forwardButton.IsEnabled = model.CanGoForward;
        }

        calendar.DisplayDate = model.DisplayDate;

        tileGrid.Clear();

        var glyphs = new Dictionary<string, string>
        {
            [EmployeeKpiKeys.PresentDays] = "✓",
            [EmployeeKpiKeys.AbsentDays] = "✕",
            [EmployeeKpiKeys.HalfDays] = "◐",
            [EmployeeKpiKeys.Holidays] = "★"
        };

        for (var index = 0; index < model.Tiles.Count; index++)
        {
            var kpi = model.Tiles[index];

            tileGrid.Add(
                new StatTile { Kpi = kpi, Glyph = glyphs.GetValueOrDefault(kpi.Key, "•") },
                index % 2,
                index / 2);
        }

        // Forces the cell template to re-evaluate against the newly loaded month.
        calendar.MonthView = new CalendarMonthView
        {
            FirstDayOfWeek = DayOfWeek.Sunday,
            CellTemplate = new DataTemplate(BuildCell)
        };
    }
}
