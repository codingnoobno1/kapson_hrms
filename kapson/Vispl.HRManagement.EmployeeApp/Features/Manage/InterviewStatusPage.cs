using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.EmployeeApp.Features.Shared;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.Mobile.Client.Api;
using Vispl.HRManagement.Mobile.Client.Auth;

namespace Vispl.HRManagement.EmployeeApp.Features.Manage;

/// <summary>
/// The Interview Status tab visible to HR and panel members.
/// Lists scheduled interviews with Round, date/time, mode and outcome.
/// </summary>
public sealed class InterviewStatusPage : ContentPage
{
    private static readonly Color HrBlue = Color.FromArgb("#1565C0");

    private readonly InterviewsApiClient interviews;
    private readonly MobileSession session;
    private readonly QuickActionsHost quickActions;
    private readonly AppSheet sheet = new();
    private readonly BrandAppBar appBar = new();
    private readonly VerticalStackLayout content = new() { Spacing = 0 };
    private readonly ActivityIndicator spinner = new() { Color = Color.FromArgb("#1565C0"), IsRunning = false };

    private bool myOnly;
    private string? statusFilter;

    public InterviewStatusPage(
        InterviewsApiClient interviews,
        MobileSession session,
        QuickActionsHost quickActions)
    {
        this.interviews = interviews;
        this.session = session;
        this.quickActions = quickActions;

        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        BackgroundColor = Tokens.Background;
        Title = "Interview Status";

        var tabBar = new EmployeeTabBar(EmployeeTab.InterviewStatus, session);
        tabBar.QuickActionsRequested += (_, _) => quickActions.Present(sheet);

        var scrollView = new ScrollView
        {
            Content = new Grid { Children = { content, BuildSpinnerOverlay() } }
        };

        sheet.Content = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star),
                new RowDefinition(GridLength.Auto)
            ],
            Children =
            {
                Row(appBar, 0),
                Row(BuildFilterRow(), 1),
                Row(scrollView, 2),
                Row(tabBar, 3)
            }
        };

        Content = sheet;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = LoadAsync();
    }

    private View BuildSpinnerOverlay()
    {
        return new Grid
        {
            HeightRequest = 200,
            VerticalOptions = LayoutOptions.Start,
            IsVisible = false,
            Children = { new Grid { VerticalOptions = LayoutOptions.Center, Children = { spinner } } }
        };
    }

    private View BuildFilterRow()
    {
        var filterChips = new HorizontalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(Tokens.SpaceGutter, 10)
        };

        var statuses = new[]
        {
            ("All", (string?)null),
            ("Upcoming", "Scheduled"),
            ("Completed", "Completed"),
            ("Cancelled", "Cancelled")
        };

        foreach (var (label, status) in statuses)
        {
            var chipStatus = status;
            var isActive = statusFilter == chipStatus;

            var chip = new Border
            {
                BackgroundColor = isActive ? HrBlue : Tokens.SurfaceMuted,
                StrokeThickness = 0,
                Padding = new Thickness(14, 7),
                StrokeShape = new RoundRectangle { CornerRadius = 20 },
                Content = new Label
                {
                    Text = label,
                    FontSize = 12,
                    FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = isActive ? Colors.White : Tokens.TextSecondary
                }
            };

            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) =>
            {
                statusFilter = chipStatus;
                await LoadAsync();
            };
            chip.GestureRecognizers.Add(tap);
            filterChips.Children.Add(chip);
        }

        // My interviews toggle
        var myToggle = new Border
        {
            BackgroundColor = myOnly ? HrBlue : Tokens.SurfaceMuted,
            StrokeThickness = 0,
            Padding = new Thickness(14, 7),
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Content = new Label
            {
                Text = "My Interviews",
                FontSize = 12,
                TextColor = myOnly ? Colors.White : Tokens.TextSecondary
            }
        };

        var myTap = new TapGestureRecognizer();
        myTap.Tapped += async (_, _) =>
        {
            myOnly = !myOnly;
            await LoadAsync();
        };
        myToggle.GestureRecognizers.Add(myTap);
        filterChips.Children.Add(myToggle);

        return new ScrollView
        {
            Orientation = ScrollOrientation.Horizontal,
            Content = filterChips,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Never
        };
    }

    private async Task LoadAsync()
    {
        content.Children.Clear();
        spinner.IsRunning = true;

        try
        {
            var page = await interviews.GetInterviewsAsync(myOnly, statusFilter);

            if (page.Items.Count == 0)
            {
                content.Children.Add(EmptyState());
                return;
            }

            // Group by date
            var grouped = page.Items
                .GroupBy(item => item.StartsUtc.ToLocalTime().Date)
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                content.Children.Add(DateHeader(group.Key));

                foreach (var interview in group)
                {
                    content.Children.Add(InterviewCard(interview));
                }
            }

            content.Children.Add(new BoxView { HeightRequest = 24 });
        }
        catch (Exception ex)
        {
            content.Children.Add(new Label
            {
                Text = ex.Message,
                FontSize = 13,
                TextColor = Tokens.TextSecondary,
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(24, 48)
            });
        }
        finally
        {
            spinner.IsRunning = false;
        }
    }

    private static View DateHeader(DateTime date)
    {
        var isToday = date.Date == DateTime.Today;
        var isTomorrow = date.Date == DateTime.Today.AddDays(1);
        var label = isToday ? "Today" : isTomorrow ? "Tomorrow" : date.ToString("dddd, d MMM yyyy");

        return new Label
        {
            Text = label,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold,
            TextColor = Tokens.TextSecondary,
            CharacterSpacing = 0.3,
            Margin = new Thickness(Tokens.SpaceGutter, 16, Tokens.SpaceGutter, 4)
        };
    }

    private View InterviewCard(InterviewListItemDto interview)
    {
        var tone = interview.Status.Tone;
        var localTime = interview.StartsUtc.ToLocalTime();

        // Status badge
        var statusBadge = new Border
        {
            BackgroundColor = Tokens.ForTone(tone),
            StrokeThickness = 0,
            Padding = new Thickness(8, 3),
            VerticalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = new Label
            {
                Text = interview.Status.Name,
                FontSize = 10,
                FontAttributes = FontAttributes.Bold,
                TextColor = Tokens.OnTone(tone)
            }
        };

        var titleRow = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            ColumnSpacing = 8
        };
        titleRow.Add(new Label
        {
            Text = interview.Candidate.DisplayName,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Tokens.TextPrimary,
            VerticalOptions = LayoutOptions.Center
        }, 0);
        titleRow.Add(statusBadge, 1);

        var modeEmoji = interview.Mode?.ToLowerInvariant() switch
        {
            "online" or "virtual" => "💻",
            "in-person" or "inperson" => "🏢",
            _ => "📞"
        };

        var detailsStack = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                titleRow,
                new Label { Text = interview.JobTitle, FontSize = 13, TextColor = Tokens.TextSecondary },
                new Label { Text = interview.Round, FontSize = 12, TextColor = Tokens.TextTertiary },
                new Label
                {
                    Text = $"📅 {localTime:ddd, d MMM yyyy}  🕐 {localTime:HH:mm}  {modeEmoji} {interview.Mode ?? ""}  ⏱ {interview.DurationMinutes} min",
                    FontSize = 11,
                    TextColor = Tokens.TextSecondary,
                    Margin = new Thickness(0, 4)
                }
            }
        };

        var card = new Border
        {
            BackgroundColor = Tokens.Surface,
            Stroke = Tokens.StatTileBorder,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Margin = new Thickness(Tokens.SpaceGutter, 4),
            Padding = new Thickness(14, 12),
            Content = detailsStack
        };

        // Left accent strip for interviews today/upcoming
        if (tone == "warning" || (interview.StartsUtc > DateTimeOffset.UtcNow && interview.Status.Code == "Scheduled"))
        {
            var grid = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(4), new ColumnDefinition(GridLength.Star)]
            };
            grid.Add(new BoxView { Color = HrBlue, HeightRequest = double.PositiveInfinity }, 0);
            grid.Add(detailsStack, 1);

            card.Content = grid;
            card.Padding = new Thickness(0, 12, 14, 12);
        }

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await ShowInterviewDetailAsync(interview.Id);
        card.GestureRecognizers.Add(tap);

        return card;
    }

    private async Task ShowInterviewDetailAsync(string interviewId)
    {
        sheet.Present("Interview Details", new ActivityIndicator { IsRunning = true, Color = HrBlue, Margin = 32 });

        try
        {
            var detail = await interviews.GetInterviewAsync(interviewId);
            sheet.Present("Interview Details", BuildInterviewDetailContent(detail));
        }
        catch (Exception ex)
        {
            sheet.Present("Interview Details", SheetContent.FieldCard(("Error", ex.Message)));
        }
    }

    private static View BuildInterviewDetailContent(InterviewDetailDto detail)
    {
        var s = detail.Summary;
        var localStart = s.StartsUtc.ToLocalTime();
        var localEnd = localStart.AddMinutes(s.DurationMinutes);

        var body = new VerticalStackLayout { Spacing = 0, Padding = new Thickness(0, 4, 0, 24) };

        body.Add(SheetContent.FieldCard(
            ("Candidate", s.Candidate.DisplayName),
            ("Job", s.JobTitle),
            ("Round", s.Round),
            ("Status", s.Status.Name)));

        body.Add(SheetContent.SectionHeader("Schedule"));
        body.Add(SheetContent.FieldCard(
            ("Date", localStart.ToString("dddd, d MMMM yyyy")),
            ("Time", $"{localStart:HH:mm} – {localEnd:HH:mm}"),
            ("Duration", $"{s.DurationMinutes} minutes"),
            ("Mode", s.Mode)));

        if (!string.IsNullOrWhiteSpace(detail.Location))
        {
            body.Add(SheetContent.FieldCard(("Location / Link", detail.Location)));
        }
        else if (!string.IsNullOrWhiteSpace(detail.JoinUrl))
        {
            body.Add(SheetContent.FieldCard(("Join Link", detail.JoinUrl)));
        }

        if (detail.Panel.Count > 0)
        {
            body.Add(SheetContent.SectionHeader("Interview Panel"));
            body.Add(SheetContent.FieldCard(
                detail.Panel.Select(p => (p.DisplayName, (string?)p.EmployeeNumber)).ToArray()));
        }

        return body;
    }

    private static View EmptyState()
    {
        return new VerticalStackLayout
        {
            Spacing = 8,
            Padding = new Thickness(Tokens.SpaceGutter, 48),
            HorizontalOptions = LayoutOptions.Center,
            Children =
            {
                new Label { Text = "📅", FontSize = 40, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = "No interviews found.", FontSize = 15, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary, HorizontalTextAlignment = TextAlignment.Center },
                new Label { Text = "Scheduled interviews will appear here.", FontSize = 13, TextColor = Tokens.TextSecondary, HorizontalTextAlignment = TextAlignment.Center }
            }
        };
    }

    private static View Row(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }
}
