using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.EmployeeApp.Shared.ViewModels;

namespace Vispl.HRManagement.EmployeeApp.Shared.Controls;

/// <summary>
/// EW-001. The white app bar: KAPSONS wordmark left, circular search and bell right.
///
/// The bell's badge hides at zero rather than showing a nought, and it is a real count from
/// the notifications endpoint — the mockup's red "5" is not decorative.
/// </summary>
public sealed class BrandAppBar : ContentView
{
    private readonly Label badgeLabel = new()
    {
        FontSize = 10,
        FontAttributes = FontAttributes.Bold,
        TextColor = Colors.White,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    private readonly Border badge;

    public BrandAppBar()
    {
        var wordmark = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = "KAPSONS",
                    FontSize = 20,
                    FontAttributes = FontAttributes.Bold,
                    CharacterSpacing = 2,
                    TextColor = Tokens.TextPrimary
                },
                new Label
                {
                    Text = "DOOR TO GLOBAL FASHION",
                    FontSize = 6,
                    CharacterSpacing = 1,
                    TextColor = Tokens.TextSecondary
                }
            }
        };

        badge = new Border
        {
            WidthRequest = 16,
            HeightRequest = 16,
            BackgroundColor = Tokens.BrandAccent,
            StrokeThickness = 0,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.End,
            VerticalOptions = LayoutOptions.Start,
            TranslationX = 4,
            TranslationY = -4,
            StrokeShape = new RoundRectangle { CornerRadius = 8 },
            Content = badgeLabel
        };

        var bell = new Grid { Children = { CircleButton("\U0001F514", OnNotificationsTapped), badge } };

        Content = new Grid
        {
            Padding = new Thickness(Tokens.SpaceGutter, 10),
            BackgroundColor = Tokens.Surface,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 8,
            Children =
            {
                wordmark,
                Place(CircleButton("\U0001F50D", OnSearchTapped), 1),
                Place(bell, 2)
            }
        };
    }

    public static readonly BindableProperty UnreadCountProperty = BindableProperty.Create(
        nameof(UnreadCount), typeof(int), typeof(BrandAppBar), 0, propertyChanged: OnUnreadChanged);

    public int UnreadCount
    {
        get => (int)GetValue(UnreadCountProperty);
        set => SetValue(UnreadCountProperty, value);
    }

    private static void OnUnreadChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var bar = (BrandAppBar)bindable;
        var count = (int)newValue;

        bar.badge.IsVisible = count > 0;
        bar.badgeLabel.Text = count > 99 ? "99+" : count.ToString();

        SemanticProperties.SetDescription(
            bar.badge, count == 1 ? "1 unread notification" : $"{count} unread notifications");
    }

    private static View Place(View view, int column)
    {
        Grid.SetColumn(view, column);
        return view;
    }

    /// <summary>EW-003. A 40pt circular action, which clears the 44pt target with its padding.</summary>
    private static View CircleButton(string glyph, EventHandler handler)
    {
        var border = new Border
        {
            WidthRequest = 40,
            HeightRequest = 40,
            BackgroundColor = Tokens.SurfaceMuted,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Content = new Label
            {
                Text = glyph,
                FontSize = 16,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += (sender, args) => handler(sender, args);
        border.GestureRecognizers.Add(tap);

        return border;
    }

    private async void OnNotificationsTapped(object? sender, EventArgs e) =>
        await Shell.Current.GoToAsync("notifications");

    private async void OnSearchTapped(object? sender, EventArgs e) =>
        await Shell.Current.DisplayAlert(
            "Search",
            "Search is not available yet. The module behind it has not been built.",
            "OK");
}

/// <summary>
/// EW-020 and EW-021. The Menu tab's section header and row.
///
/// The row is the tab's only content type: icon tile, title, one-line subtitle, chevron.
/// </summary>
public sealed class MenuSectionHeader : ContentView
{
    public MenuSectionHeader(string emoji, string text)
    {
        Content = new HorizontalStackLayout
        {
            Spacing = 6,
            Padding = new Thickness(Tokens.SpaceGutter, Tokens.SpaceSectionGap, Tokens.SpaceGutter, 8),
            Children =
            {
                new Label { Text = emoji, FontSize = Tokens.FontSectionHeader },
                new Label
                {
                    Text = text.ToUpperInvariant(),
                    FontSize = Tokens.FontSectionHeader,
                    FontAttributes = FontAttributes.Bold,
                    CharacterSpacing = 1,
                    TextColor = Tokens.TextSecondary,
                    VerticalTextAlignment = TextAlignment.Center
                }
            }
        };

        SemanticProperties.SetHeadingLevel(this, SemanticHeadingLevel.Level2);
        SemanticProperties.SetDescription(this, text);
    }
}

public sealed class MenuRow : ContentView
{
    /// <summary>
    /// A row whose feature is not built yet still appears, and says so when tapped. Hiding it
    /// would make the app look smaller than the plan; showing it broken would be worse.
    /// </summary>
    public MenuRow(string glyph, string title, string subtitle, Func<Task>? onTap, bool isReady = true)
    {
        var iconTile = new Border
        {
            WidthRequest = 32,
            HeightRequest = 32,
            // 1.png: a light grey rounded square behind a full-colour emoji, for every row.
            BackgroundColor = Tokens.MenuIconTile,
            Opacity = isReady ? 1 : 0.6,
            StrokeThickness = 0,
            VerticalOptions = LayoutOptions.Center,
            StrokeShape = new RoundRectangle { CornerRadius = 10 },
            Content = new Label
            {
                Text = glyph,
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var text = new VerticalStackLayout
        {
            Spacing = 1,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                new Label
                {
                    Text = title,
                    FontSize = Tokens.FontRowTitle,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = isReady ? Tokens.TextPrimary : Tokens.TextSecondary
                },
                new Label
                {
                    Text = subtitle,
                    FontSize = Tokens.FontRowSubtitle,
                    TextColor = Tokens.TextSecondary
                }
            }
        };

        var chevron = new Label
        {
            Text = "›",
            FontSize = 20,
            TextColor = Tokens.TextSecondary,
            VerticalTextAlignment = TextAlignment.Center
        };

        var grid = new Grid
        {
            Padding = new Thickness(14, 12),
            ColumnSpacing = 12,
            MinimumHeightRequest = Tokens.MinimumTouchTarget,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Auto),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            ]
        };

        grid.Add(iconTile, 0);
        grid.Add(text, 1);
        grid.Add(chevron, 2);

        Content = new Border
        {
            Stroke = Tokens.Border,
            StrokeThickness = 1,
            BackgroundColor = Tokens.Surface,
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 8),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Content = grid
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            if (onTap is not null)
            {
                await onTap();
            }
            else
            {
                await Shell.Current.DisplayAlert(
                    title,
                    $"{title} is not available yet. The module behind it has not been built.",
                    "OK");
            }
        };

        GestureRecognizers.Add(tap);

        SemanticProperties.SetDescription(this, $"{title}. {subtitle}");
    }
}

/// <summary>
/// EW-100 to EW-105. The six states, in one control the pages drop in once.
///
/// Building this before any content widget is what stops a screen being written with only a
/// happy path — there is nowhere to put the loading case except here.
/// </summary>
public sealed class StateView : ContentView
{
    private readonly Label headline = new()
    {
        FontSize = 16,
        FontAttributes = FontAttributes.Bold,
        HorizontalTextAlignment = TextAlignment.Center,
        TextColor = Tokens.TextPrimary
    };

    private readonly Label detail = new()
    {
        FontSize = 13,
        HorizontalTextAlignment = TextAlignment.Center,
        TextColor = Tokens.TextSecondary
    };

    private readonly Label glyph = new()
    {
        FontSize = 30,
        HorizontalTextAlignment = TextAlignment.Center
    };

    private readonly ActivityIndicator spinner = new()
    {
        Color = Tokens.BrandAccent,
        IsVisible = false,
        IsRunning = false,
        HorizontalOptions = LayoutOptions.Center
    };

    private readonly Button retry;

    public StateView()
    {
        retry = new Button
        {
            Text = "Try again",
            BackgroundColor = Tokens.BrandAccent,
            TextColor = Tokens.OnAccent,
            CornerRadius = 10,
            HeightRequest = Tokens.MinimumTouchTarget,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Center
        };

        retry.Clicked += (_, _) => RetryRequested?.Invoke(this, EventArgs.Empty);

        Content = new VerticalStackLayout
        {
            Spacing = 10,
            Padding = 32,
            VerticalOptions = LayoutOptions.Center,
            Children = { spinner, glyph, headline, detail, retry }
        };
    }

    public event EventHandler? RetryRequested;

    public static readonly BindableProperty ViewModelProperty = BindableProperty.Create(
        nameof(ViewModel), typeof(ViewModelBase), typeof(StateView), propertyChanged: OnViewModelChanged);

    public ViewModelBase? ViewModel
    {
        get => (ViewModelBase?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private static void OnViewModelChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var view = (StateView)bindable;

        if (oldValue is ViewModelBase previous)
        {
            previous.PropertyChanged -= view.OnModelChanged;
        }

        if (newValue is ViewModelBase next)
        {
            next.PropertyChanged += view.OnModelChanged;
            view.Render(next);
        }
    }

    private void OnModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (sender is ViewModelBase model &&
            e.PropertyName is nameof(ViewModelBase.State) or nameof(ViewModelBase.Message))
        {
            MainThread.BeginInvokeOnMainThread(() => Render(model));
        }
    }

    private void Render(ViewModelBase model)
    {
        IsVisible = model.State != ScreenState.Ready;

        spinner.IsVisible = model.State == ScreenState.Loading;
        spinner.IsRunning = model.State == ScreenState.Loading;
        glyph.IsVisible = model.State != ScreenState.Loading;

        // Retry is offered only where retrying can plausibly work. A 501 will answer 501 again,
        // and a permission failure will keep failing, so neither gets a button that does nothing.
        retry.IsVisible = model.State is ScreenState.Error or ScreenState.Offline;

        (glyph.Text, headline.Text, detail.Text) = model.State switch
        {
            ScreenState.Loading => (string.Empty, "Loading", string.Empty),

            ScreenState.Empty => ("\U0001F4ED", "Nothing here yet", model.Message ?? string.Empty),

            // The not-ready panel names the feature. This is the difference between
            // "Salary is not available yet" and a crash report.
            ScreenState.NotReady => (
                "\U0001F6A7",
                $"{model.NotReadyModule ?? "This feature"} is not available yet",
                "It has not been built. Nothing is wrong with your account."),

            ScreenState.Denied => ("\U0001F512", "You do not have access to this",
                model.Message ?? "Ask HR if you think you should."),

            ScreenState.Offline => ("\U0001F4F6", "No connection",
                model.Message ?? "Check your signal and try again."),

            _ => ("⚠️", "Something went wrong", model.Message ?? string.Empty)
        };

        detail.IsVisible = !string.IsNullOrWhiteSpace(detail.Text);
    }
}
