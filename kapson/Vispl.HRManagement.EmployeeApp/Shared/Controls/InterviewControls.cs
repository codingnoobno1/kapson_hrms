using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Recruitment;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;

namespace Vispl.HRManagement.EmployeeApp.Shared.Controls;

/// <summary>
/// The Home "Interview Schedule" card from 5.png: a white card with a chat icon, the title, a
/// soft coral count badge and a chevron that collapses the rows.
///
/// Interviewers only (build plan D3). Hidden entirely for everyone else, so an employee who does
/// not interview never sees an empty recruiter card.
/// </summary>
public sealed class InterviewScheduleCard : ContentView
{
    private readonly Label countLabel = new()
    {
        FontSize = 13,
        FontAttributes = FontAttributes.Bold,
        TextColor = Tokens.BrandAccent,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    private readonly Label chevron = new()
    {
        Text = "⌃",
        FontSize = 16,
        TextColor = Tokens.TextSecondary,
        VerticalTextAlignment = TextAlignment.Center
    };

    private readonly VerticalStackLayout rows = new() { Spacing = 10 };
    private bool expanded = true;

    public InterviewScheduleCard()
    {
        IsVisible = false;

        var badge = new Border
        {
            WidthRequest = 30,
            HeightRequest = 30,
            BackgroundColor = Tokens.CountBadgeTint,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 15 },
            Content = countLabel
        };

        var header = new Grid
        {
            ColumnSpacing = 10,
            MinimumHeightRequest = Tokens.MinimumTouchTarget,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            ]
        };

        header.Add(Icons.Create(Icons.Chat, Tokens.TextPrimary, 20), 0);
        header.Add(new Label
        {
            Text = "Interview Schedule",
            FontSize = 16,
            TextColor = Tokens.TextPrimary,
            VerticalTextAlignment = TextAlignment.Center
        }, 1);
        header.Add(badge, 2);
        header.Add(chevron, 3);

        var toggle = new TapGestureRecognizer();
        toggle.Tapped += (_, _) =>
        {
            expanded = !expanded;
            rows.IsVisible = expanded;
            chevron.Text = expanded ? "⌃" : "⌄";
        };
        header.GestureRecognizers.Add(toggle);

        SemanticProperties.SetHeadingLevel(header, SemanticHeadingLevel.Level2);

        Content = new Border
        {
            Stroke = Tokens.Border,
            StrokeThickness = 1,
            BackgroundColor = Tokens.Surface,
            Padding = new Thickness(14, 8, 14, 14),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Shadow = new Shadow { Brush = new SolidColorBrush(Colors.Black), Opacity = 0.05f, Radius = 8, Offset = new Point(0, 2) },
            Content = new VerticalStackLayout { Spacing = 8, Children = { header, rows } }
        };
    }

    public void Bind(EmployeeInterviewScheduleDto? schedule)
    {
        IsVisible = schedule is { IsInterviewer: true };

        if (schedule is null || !schedule.IsInterviewer)
        {
            return;
        }

        countLabel.Text = schedule.Interviews.Count.ToString();
        rows.Clear();

        foreach (var interview in schedule.Interviews.Take(6))
        {
            rows.Add(new InterviewRow(interview));
        }

        if (schedule.Interviews.Count == 0)
        {
            rows.Add(new Label
            {
                Text = "Nothing on your interview panel in the next two weeks.",
                FontSize = 13,
                TextColor = Tokens.TextSecondary
            });
        }

        SemanticProperties.SetDescription(this, $"Interview schedule, {schedule.Interviews.Count} interviews");
    }
}

/// <summary>
/// One interview in the card: pale coral plate with a coral left rule, candidate and role, a
/// status pill, then date, time and round.
/// </summary>
public sealed class InterviewRow : ContentView
{
    public InterviewRow(InterviewListItemDto interview)
    {
        var local = interview.StartsUtc.ToLocalTime();
        var isPast = interview.StartsUtc.AddMinutes(interview.DurationMinutes) < DateTimeOffset.UtcNow;

        var pill = new Border
        {
            BackgroundColor = isPast ? Tokens.PastInterviewPill : Tokens.StatusInfo,
            StrokeThickness = 0,
            Padding = new Thickness(10, 3),
            VerticalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusPill },
            Content = new Label
            {
                Text = interview.Status.Name,
                FontSize = 11,
                TextColor = Colors.White
            }
        };

        var top = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };

        top.Add(new Label
        {
            Text = interview.Candidate.DisplayName,
            FontSize = 15,
            FontAttributes = FontAttributes.Bold,
            TextColor = Tokens.TextPrimary
        }, 0);
        top.Add(pill, 1);

        var details = new VerticalStackLayout
        {
            Spacing = 4,
            Padding = new Thickness(14, 10, 12, 10),
            Children =
            {
                top,
                new Label { Text = interview.JobTitle, FontSize = 13, TextColor = Tokens.TextSecondary },
                new Label
                {
                    Text = $"\U0001F4C5 {local:yyyy-MM-dd}      ⏰ {local:HH:mm}",
                    FontSize = 12,
                    TextColor = Tokens.TextPrimary
                },
                new Label { Text = interview.Round, FontSize = 12, TextColor = Tokens.TextPrimary }
            }
        };

        var plate = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(new GridLength(3)), new ColumnDefinition(GridLength.Star)]
        };

        plate.Add(new BoxView { Color = Tokens.InterviewRule }, 0);
        plate.Add(details, 1);

        Content = new Border
        {
            BackgroundColor = Tokens.InterviewRowTint,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = plate
        };

        SemanticProperties.SetDescription(
            this,
            $"{interview.Candidate.DisplayName}, {interview.JobTitle}, {interview.Status.Name}, {local:d MMMM} at {local:HH:mm}, {interview.Round}");
    }
}
