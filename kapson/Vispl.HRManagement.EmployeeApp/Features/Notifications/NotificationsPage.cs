using System.Collections.ObjectModel;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Sheets;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.EmployeeApp.Shared.ViewModels;
using Vispl.HRManagement.Mobile.Client.Api;

namespace Vispl.HRManagement.EmployeeApp.Features.Notifications;

/// <summary>
/// EMP-060. What has happened on the things this person asked for.
///
/// This screen is only worth having because leave, attendance, gate passes, movement, profile
/// changes and mobile access all publish now. Before that it carried recruitment traffic alone,
/// and an employee never heard back about their own request.
/// </summary>
public sealed class NotificationsViewModel(NotificationsApiClient notifications) : ViewModelBase
{
    public ObservableCollection<NotificationListItemDto> Items { get; } = [];

    public override async Task LoadAsync(CancellationToken ct = default)
    {
        await RunAsync(async () =>
        {
            var page = await notifications.ListAsync(null, ct);

            Items.Clear();
            foreach (var item in page.Items.OrderByDescending(entry => entry.OccurredUtc))
            {
                Items.Add(item);
            }

            if (Items.Count == 0)
            {
                Empty("Nothing has happened on your requests yet.");
                return;
            }

            Ready();
        });
    }

    public async Task MarkReadAsync(NotificationListItemDto item)
    {
        if (item.IsRead)
        {
            return;
        }

        try
        {
            await notifications.MarkReadAsync([item.Id]);
        }
        catch (Exception exception)
        {
            // Failing to mark read must not block opening the notification. The worst case is
            // that it stays bold and is marked again next time.
            System.Diagnostics.Debug.WriteLine($"Mark read failed: {exception.Message}");
        }
    }
}

public sealed class NotificationsPage : ContentPage
{
    private readonly NotificationsViewModel model;
    private readonly NotificationsApiClient notifications;
    private readonly VerticalStackLayout list = new();
    private readonly StateView stateView = new();
    private readonly RefreshView refresher;
    private readonly AppSheet sheet = new();

    public NotificationsPage(NotificationsViewModel model, NotificationsApiClient notifications)
    {
        this.model = model;
        this.notifications = notifications;

        Title = "Notifications";
        BackgroundColor = Tokens.Background;

        stateView.ViewModel = model;
        stateView.RetryRequested += async (_, _) => await model.LoadAsync();

        refresher = new RefreshView
        {
            RefreshColor = Tokens.BrandAccent,
            Content = new ScrollView
            {
                Content = new Grid { Children = { list, stateView } }
            }
        };

        refresher.Refreshing += async (_, _) =>
        {
            await model.RefreshAsync();
            refresher.IsRefreshing = false;
        };

        sheet.Content = refresher;
        Content = sheet;

        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ViewModelBase.State))
            {
                MainThread.BeginInvokeOnMainThread(Render);
            }
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await model.LoadAsync();
    }

    private void Render()
    {
        list.Clear();
        list.IsVisible = model.IsReady;

        foreach (var item in model.Items)
        {
            list.Add(BuildRow(item));
        }
    }

    private View BuildRow(NotificationListItemDto item)
    {
        var unreadDot = new Border
        {
            WidthRequest = 8,
            HeightRequest = 8,
            BackgroundColor = Tokens.BrandAccent,
            StrokeThickness = 0,
            IsVisible = !item.IsRead,
            VerticalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, 6, 0, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 4 }
        };

        var text = new VerticalStackLayout
        {
            Spacing = 3,
            Children =
            {
                new Label
                {
                    Text = item.Title,
                    FontSize = Tokens.FontRowTitle,
                    FontAttributes = item.IsRead ? FontAttributes.None : FontAttributes.Bold,
                    TextColor = Tokens.TextPrimary
                },
                new Label
                {
                    Text = item.Preview,
                    FontSize = Tokens.FontRowSubtitle,
                    TextColor = Tokens.TextSecondary,
                    MaxLines = 2,
                    LineBreakMode = LineBreakMode.TailTruncation
                },
                new Label
                {
                    Text = $"{item.Category} · {Relative(item.OccurredUtc)}",
                    FontSize = 11,
                    TextColor = Tokens.TextSecondary
                }
            }
        };

        var grid = new Grid
        {
            Padding = new Thickness(14, 12),
            ColumnSpacing = 10,
            MinimumHeightRequest = Tokens.MinimumTouchTarget,
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)]
        };

        grid.Add(unreadDot, 0);
        grid.Add(text, 1);

        var card = new Border
        {
            Stroke = Tokens.Border,
            StrokeThickness = 1,
            BackgroundColor = item.IsRead ? Tokens.Surface : Tokens.BrandAccentTint,
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 8),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Content = grid
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await OpenAsync(item);
        card.GestureRecognizers.Add(tap);

        SemanticProperties.SetDescription(
            card, $"{(item.IsRead ? string.Empty : "Unread. ")}{item.Title}. {item.Preview}");

        return card;
    }

    private async Task OpenAsync(NotificationListItemDto item)
    {
        await model.MarkReadAsync(item);

        try
        {
            var detail = await notifications.GetAsync(item.Id);

            sheet.Present(detail.Title, new VerticalStackLayout
            {
                Spacing = 0,
                Children =
                {
                    new Label
                    {
                        Text = detail.Body,
                        FontSize = 14,
                        TextColor = Tokens.TextPrimary,
                        Margin = new Thickness(Tokens.SpaceGutter, 4, Tokens.SpaceGutter, 16)
                    },
                    SheetContent.FieldCard(
                        ("Category", detail.Category),
                        ("When", item.OccurredUtc.ToLocalTime().ToString("d MMMM yyyy, HH:mm")))
                }
            });
        }
        catch (Exception exception)
        {
            System.Diagnostics.Debug.WriteLine(exception);
            sheet.Present(item.Title, SheetContent.FieldCard(("Details", item.Preview)));
        }

        await model.LoadAsync();
    }

    private static string Relative(DateTimeOffset occurred)
    {
        var elapsed = DateTimeOffset.UtcNow - occurred;

        return elapsed switch
        {
            { TotalMinutes: < 1 } => "just now",
            { TotalMinutes: < 60 } => $"{(int)elapsed.TotalMinutes}m ago",
            { TotalHours: < 24 } => $"{(int)elapsed.TotalHours}h ago",
            { TotalDays: < 7 } => $"{(int)elapsed.TotalDays}d ago",
            _ => occurred.ToLocalTime().ToString("d MMM")
        };
    }
}
