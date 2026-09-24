using Microsoft.Maui.Controls.Shapes;
using Syncfusion.Maui.Charts;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.EmployeeApp.Features.Shared;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Client.Http;

namespace Vispl.HRManagement.EmployeeApp.Features.Attendance;

/// <summary>
/// EMP-025 Attendance Analytics — the three scroll positions of the phase C screenshots on one
/// scrolling page: Select Period, Key Performance Indicators, Monthly Breakdown, Key Insights,
/// Detailed Statistics, Export Report and Email Analytics.
///
/// Nothing is calculated here: <c>me/attendance/analytics</c> returns the figures, deltas and
/// insight texts. Export saves the server's .xlsx to the cache and hands it to the share sheet;
/// the file is not kept by the app.
/// </summary>
public sealed class AttendanceAnalyticsPage : ContentPage
{
    private readonly MeApiClient me;
    private readonly AppSheet sheet = new();
    private readonly FlexLayout chips = new() { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Margin = new Thickness(Tokens.SpaceGutter, 0) };
    private readonly VerticalStackLayout body = new() { Spacing = 0 };
    private readonly ActivityIndicator busy = new() { IsRunning = true, Color = Tokens.BrandAccent, Margin = 40 };
    private readonly Label error = new() { FontSize = 13, TextColor = Tokens.StatusDanger, IsVisible = false, Margin = new Thickness(Tokens.SpaceGutter, 12) };
    private string period = AttendancePeriods.ThisMonth;

    public AttendanceAnalyticsPage(MeApiClient me, QuickActionsHost quickActions)
    {
        this.me = me;

        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        BackgroundColor = Tokens.Background;
        Title = "Attendance Analytics";

        var content = new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                AttendanceVisuals.BackHeader("Attendance Analytics", () => Shell.Current.GoToAsync("..")),
                Label("Select Period", 15, top: 0),
                chips,
                error,
                busy,
                body
            }
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
            Children =
            {
                Row(new BrandAppBar(), 0),
                Row(new ScrollView { Content = content }, 1),
                Row(tabBar, 2)
            }
        };

        Content = sheet;
        RenderChips();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        busy.IsVisible = true;
        error.IsVisible = false;
        body.Clear();

        try
        {
            var analytics = await me.GetAttendanceAnalyticsAsync(period);
            Render(analytics);
        }
        catch (MobileApiException exception)
        {
            error.Text = exception is MobileOfflineException ? "No connection to the Kapsons server." : exception.Message;
            error.IsVisible = true;
        }
        finally
        {
            busy.IsVisible = false;
        }
    }

    // ---- C06 Select Period ------------------------------------------------------

    private void RenderChips()
    {
        chips.Clear();

        foreach (var (key, label) in AttendancePeriods.All)
        {
            var active = key == period;
            var chip = new Border
            {
                BackgroundColor = active ? Tokens.BrandAccent : Tokens.PeriodChipInactive,
                StrokeThickness = 0,
                StrokeShape = new RoundRectangle { CornerRadius = 999 },
                Padding = new Thickness(14, 6),
                Margin = new Thickness(0, 0, 8, 8),
                Content = new Label
                {
                    Text = label,
                    FontSize = 13,
                    TextColor = active ? Colors.White : Tokens.PeriodChipInactiveText
                }
            };

            var captured = key;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                if (period == captured)
                {
                    return;
                }

                period = captured;
                RenderChips();
                await LoadAsync();
            };
            chip.GestureRecognizers.Add(tap);
            SemanticProperties.SetDescription(chip, active ? $"{label}, selected" : label);
            chips.Add(chip);
        }
    }

    private void Render(AttendanceAnalyticsDto analytics)
    {
        body.Clear();

        body.Add(Label("Key Performance Indicators", 15, top: 12));
        foreach (var kpi in analytics.Kpis)
        {
            body.Add(KpiBand(kpi));
        }

        body.Add(Label("Monthly Breakdown", 15, top: 16));
        body.Add(MonthlyChart(analytics.Months));

        body.Add(Label("Key Insights", 15, top: 16));
        body.Add(Insights(analytics.Insights));

        body.Add(Label("Detailed Statistics", 15, top: 16));
        body.Add(Statistics(analytics.Statistics));

        body.Add(ActionRow());
    }

    // ---- C07 KPI band card --------------------------------------------------------

    private static View KpiBand(AnalyticsKpiDto kpi)
    {
        var (ground, border, ink) = kpi.Key switch
        {
            "days-present" => (Tokens.KpiBandPresentGround, Tokens.KpiBandPresentBorder, Tokens.KpiBandPresentInk),
            "days-absent" => (Tokens.KpiBandAbsentGround, Tokens.KpiBandAbsentBorder, Tokens.KpiBandAbsentInk),
            "half-days" => (Tokens.KpiBandHalfGround, Tokens.KpiBandHalfBorder, Tokens.KpiBandHalfInk),
            _ => (Tokens.KpiBandOverallGround, Tokens.KpiBandOverallBorder, Tokens.KpiBandOverallInk)
        };

        var labelInk = kpi.Key == "overall-attendance" ? Tokens.TextSecondary : ink;

        return new Border
        {
            BackgroundColor = ground,
            Stroke = border,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Padding = new Thickness(14, 10),
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 10),
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                Children =
                {
                    new Label { Text = kpi.Label, FontSize = 12, TextColor = labelInk },
                    new Label { Text = kpi.Value, FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = ink },
                    new Label
                    {
                        Text = kpi.Delta.Text,
                        FontSize = 12,
                        TextColor = kpi.Delta.Tone == "bad" ? Tokens.DeltaBad : Tokens.DeltaGood
                    }
                }
            }
        };
    }

    // ---- C08 Monthly breakdown chart ----------------------------------------------

    private static View MonthlyChart(IReadOnlyList<AnalyticsMonthDto> months)
    {
        var points = months.Select(month => new AttendanceChartPoint(month.Label, (double)month.AttendancePercent)).ToList();
        var chart = new SfCartesianChart
        {
            HeightRequest = 210
        };
        chart.XAxes.Add(new CategoryAxis { LabelStyle = new ChartAxisLabelStyle { FontSize = 11, TextColor = Tokens.TextSecondary } });
        chart.YAxes.Add(new NumericalAxis
        {
            Minimum = 0,
            Maximum = 100,
            Interval = 25,
            LabelStyle = new ChartAxisLabelStyle { FontSize = 10, TextColor = Tokens.TextTertiary }
        });
        chart.Series.Add(new ColumnSeries
        {
            ItemsSource = points,
            XBindingPath = nameof(AttendanceChartPoint.Month),
            YBindingPath = nameof(AttendanceChartPoint.Percent),
            Fill = new SolidColorBrush(Tokens.ChartBar),
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            EnableTooltip = true,
            ShowDataLabels = true
        });

        return InfoCard(new VerticalStackLayout
        {
            Spacing = 0,
            Children =
            {
                new Label { Text = $"Attendance Summary - Last {months.Count} Months", FontSize = 15, TextColor = Tokens.TextSecondary },
                chart,
                new Label { Text = "Tap a column for the exact percentage", FontSize = 11, TextColor = Tokens.TextTertiary, HorizontalTextAlignment = TextAlignment.Center }
            }
        });
    }

    private sealed record AttendanceChartPoint(string Month, double Percent);

    // ---- C09 Insights ---------------------------------------------------------------

    private static View Insights(IReadOnlyList<AnalyticsInsightDto> insights)
    {
        var stack = new VerticalStackLayout
        {
            Spacing = 0,
            Children = { new Label { Text = "Analytics & Recommendations", FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#374151"), Margin = new Thickness(0, 0, 0, 8) } }
        };

        for (var index = 0; index < insights.Count; index++)
        {
            var insight = insights[index];
            var grid = new Grid
            {
                ColumnSpacing = 8,
                Padding = new Thickness(0, 6),
                ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)]
            };

            grid.Add(new Label { Text = insight.Emoji, FontSize = 14, VerticalOptions = LayoutOptions.Start }, 0);
            grid.Add(new VerticalStackLayout
            {
                Spacing = 1,
                Children =
                {
                    new Label { Text = insight.Title, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary },
                    new Label { Text = insight.Text, FontSize = 12, TextColor = Tokens.TextSecondary }
                }
            }, 1);

            stack.Add(grid);

            if (index < insights.Count - 1)
            {
                stack.Add(new BoxView { HeightRequest = 1, Color = Tokens.InfoDivider });
            }
        }

        return InfoCard(stack);
    }

    // ---- C10 Detailed statistics ------------------------------------------------------

    private static View Statistics(AnalyticsStatisticsDto stats)
    {
        var rows = new (string Label, string Value, Color Ink, bool Bold)[]
        {
            ("Total Working Days", stats.WorkingDays.ToString(), Tokens.TextPrimary, true),
            ("Days Present", stats.Present.ToString(), Tokens.StatsPresent, false),
            ("Days Absent", stats.Absent.ToString(), Tokens.StatsAbsent, false),
            ("Half Days", stats.HalfDays.ToString(), Tokens.StatsHalf, false),
            ("Attendance Rate", $"{stats.AttendanceRate:0.#}%", Tokens.TextPrimary, true)
        };

        var stack = new VerticalStackLayout { Spacing = 0 };

        for (var index = 0; index < rows.Length; index++)
        {
            var (label, value, ink, bold) = rows[index];
            var grid = new Grid
            {
                Padding = new Thickness(14, 11),
                ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
            };

            grid.Add(new Label
            {
                Text = label,
                FontSize = 14,
                FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                TextColor = bold ? Tokens.TextPrimary : Color.FromArgb("#374151")
            }, 0);
            grid.Add(new Label { Text = value, FontSize = 14, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None, TextColor = ink }, 1);
            stack.Add(grid);

            if (index < rows.Length - 1)
            {
                stack.Add(new BoxView { HeightRequest = 1, Color = Tokens.StatsDivider, Margin = new Thickness(14, 0) });
            }
        }

        return new Border
        {
            BackgroundColor = Tokens.StatsPlate,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Margin = new Thickness(Tokens.SpaceGutter, 0),
            Content = stack
        };
    }

    // ---- C11 Export / Email ---------------------------------------------------------

    private View ActionRow()
    {
        var grid = new Grid
        {
            ColumnSpacing = Tokens.SpaceCardGap,
            Margin = new Thickness(Tokens.SpaceGutter, Tokens.SpaceSectionGap, Tokens.SpaceGutter, 28),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)]
        };

        grid.Add(AttendanceVisuals.IconButton(AttendanceVisuals.IconDownload, "Export Report", Tokens.SecondaryButton, Tokens.TextPrimary, ExportAsync), 0);
        grid.Add(AttendanceVisuals.IconButton(AttendanceVisuals.IconMail, "Email Analytics", Tokens.BrandAccent, Colors.White, EmailAsync), 1);
        return grid;
    }

    private async Task ExportAsync()
    {
        try
        {
            var report = await me.GetAttendanceReportAsync(period);
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
            await DisplayAlert("Export failed", exception.Message, "OK");
        }
    }

    private async Task EmailAsync()
    {
        try
        {
            var result = await me.EmailAttendanceReportAsync(period);
            await DisplayAlert(
                result.Sent ? "Analytics sent" : "Not sent",
                result.Sent ? $"Sent to {result.SentTo}." : result.Failure ?? "The email could not be sent.",
                "OK");
        }
        catch (MobileApiException exception)
        {
            await DisplayAlert("Email failed", exception.Message, "OK");
        }
    }

    // ---- Parts ------------------------------------------------------------------------

    private static View InfoCard(View content) => new Border
    {
        BackgroundColor = Tokens.InfoCardGround,
        Stroke = Tokens.InfoCardBorder,
        StrokeThickness = 1,
        StrokeShape = new RoundRectangle { CornerRadius = 8 },
        Padding = new Thickness(14, 12),
        Margin = new Thickness(Tokens.SpaceGutter, 0),
        Content = content
    };

    private static Label Label(string text, double size, double top) => new()
    {
        Text = text,
        FontSize = size,
        FontAttributes = FontAttributes.Bold,
        TextColor = Tokens.TextPrimary,
        Margin = new Thickness(Tokens.SpaceGutter, top, Tokens.SpaceGutter, 8)
    };

    private static View Row(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }
}
