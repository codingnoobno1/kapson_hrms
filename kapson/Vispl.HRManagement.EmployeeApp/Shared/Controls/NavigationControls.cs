using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Vispl.HRManagement.Mobile.Client.Auth;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Vispl.HRManagement.EmployeeApp.Shared.Controls;

/// <summary>
/// The destinations on the bottom bar. Regular employees see Home/Leave/Attendance/Menu;
/// HR users (RECRUITMENT.VIEW) see Home/Manage/InterviewStatus/Menu instead.
/// </summary>
public enum EmployeeTab
{
    Home,
    Leave,
    Attendance,
    Menu,
    Manage,
    InterviewStatus
}

/// <summary>
/// EW-004 and EW-005. The bottom bar of 1.png / 5.png: Home · Leave · (+) · Attendance · Menu,
/// with the coral action button raised over the bar's top edge.
///
/// MAUI's Shell tab bar cannot host a control that overlaps it, so the stock bar is hidden and
/// each tab root places one of these as its last row. Slots navigate with absolute Shell routes,
/// so switching tabs never stacks pages. The image's third slot said Projects; it is Leave
/// (build plan D1).
/// </summary>
public sealed class EmployeeTabBar : ContentView
{
    // Outline icons as path data, so they tint with the slot state the way the image's line icons
    // do. Emoji cannot be recoloured, and the app ships no icon font.
    private const string HomeIcon = "M3,11 L12,3.5 L21,11 M5.5,9.5 L5.5,20.5 L10,20.5 L10,14.5 L14,14.5 L14,20.5 L18.5,20.5 L18.5,9.5";
    private const string LeaveIcon = "M7,3 L14.5,3 L19,7.5 L19,21 L7,21 Z M14.5,3 L14.5,7.5 L19,7.5 M10,12 L16,12 M10,16 L16,16";
    private const string AttendanceIcon = "M12,4 A3.5,3.5 0 1 1 11.99,4 Z M5,20.5 C5,15.5 8,13.5 12,13.5 C16,13.5 19,15.5 19,20.5 M15.5,17 L17.2,18.7 L20.5,15.4";
    private const string MenuIcon = "M4,7 L20,7 M4,12 L20,12 M4,17 L20,17";
    private const string ManageIcon = "M9,3 H5 C3.9,3 3,3.9 3,5 V9 C3,10.1 3.9,11 5,11 H9 C10.1,11 11,10.1 11,9 V5 C11,3.9 10.1,3 9,3 Z M19,3 H15 C13.9,3 13,3.9 13,5 V9 C13,10.1 13.9,11 15,11 H19 C20.1,11 21,10.1 21,9 V15 C21,14 20.1,13 19,13 H15 C13.9,13 13,13.9 13,15 V19 C13,20.1 13.9,21 15,21 H19 C20.1,21 21,20.1 21,19 V15 C21,13.9 20.1,13 19,13";
    private const string InterviewIcon = "M17,12 H15 V7 H17 V12 Z M12,12 V5 H10 V12 H12 Z M12,21 L8,17 H3 V3 H21 V17 H16 L12,21 Z";

    /// <summary>Raised when the coral (+) is tapped. The page presents the quick actions sheet.</summary>
    public event EventHandler? QuickActionsRequested;

    private readonly Border leaveDot;

    /// <param name="active">Which tab to highlight as selected.</param>
    /// <param name="session">
    /// When supplied, users with RECRUITMENT.VIEW see Manage / Interview Status instead of
    /// Leave / Attendance (the reference mockup's HR user navigation).
    /// </param>
    public EmployeeTabBar(EmployeeTab active, MobileSession? session = null)
    {
        var hrMode = session?.Has("RECRUITMENT.VIEW") ?? false;

        var bar = new Grid
        {
            HeightRequest = Tokens.TabBarHeight,
            BackgroundColor = Tokens.Surface,
            VerticalOptions = LayoutOptions.End,
            ColumnDefinitions =
            [
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(Tokens.FabSize + 24)),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            ]
        };

        leaveDot = new Border
        {
            WidthRequest = 8,
            HeightRequest = 8,
            BackgroundColor = Tokens.BrandAccent,
            StrokeThickness = 0,
            IsVisible = false,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            TranslationX = 13,
            Margin = new Thickness(0, 8, 0, 0),
            StrokeShape = new RoundRectangle { CornerRadius = 4 }
        };

        bar.Add(Slot(HomeIcon, "Home", "//home", active == EmployeeTab.Home), 0);

        if (hrMode)
        {
            bar.Add(Slot(ManageIcon, "Manage", "//manage", active == EmployeeTab.Manage), 1);
            bar.Add(Slot(InterviewIcon, "Interview\nStatus", "//interview-status", active == EmployeeTab.InterviewStatus), 3);
        }
        else
        {
            bar.Add(new Grid { Children = { Slot(LeaveIcon, "Leave", "//leave", active == EmployeeTab.Leave), leaveDot } }, 1);
            bar.Add(Slot(AttendanceIcon, "Attendance", "//attendance", active == EmployeeTab.Attendance), 3);
        }

        bar.Add(Slot(MenuIcon, "Menu", "//menu", active == EmployeeTab.Menu), 4);

        var rule = new BoxView
        {
            HeightRequest = 1,
            Color = Tokens.TabBarRule,
            VerticalOptions = LayoutOptions.End,
            Margin = new Thickness(0, 0, 0, Tokens.TabBarHeight)
        };

        var fab = new Border
        {
            WidthRequest = Tokens.FabSize,
            HeightRequest = Tokens.FabSize,
            BackgroundColor = Tokens.BrandAccent,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.FabSize / 2 },
            Shadow = new Shadow
            {
                Brush = new SolidColorBrush(Tokens.BrandAccent),
                Opacity = 0.35f,
                Radius = 14,
                Offset = new Point(0, 5)
            },
            Content = new Label
            {
                Text = "+",
                FontSize = 32,
                TextColor = Tokens.OnAccent,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                Margin = new Thickness(0, -3, 0, 0)
            }
        };

        var fabTap = new TapGestureRecognizer();
        fabTap.Tapped += (_, _) => QuickActionsRequested?.Invoke(this, EventArgs.Empty);
        fab.GestureRecognizers.Add(fabTap);

        SemanticProperties.SetDescription(fab, "Quick actions");
        AutomationId = "EmployeeTabBar";

        // One cell: the bar sits at the bottom, the button rides 20pt above its top edge.
        Content = new Grid
        {
            HeightRequest = Tokens.TabBarHeight + 22,
            BackgroundColor = Colors.Transparent,
            Children = { rule, bar, fab }
        };
    }

    /// <summary>A coral dot on the Leave slot when approvals are waiting for this person.</summary>
    public bool HasLeaveAttention
    {
        get => leaveDot.IsVisible;
        set => leaveDot.IsVisible = value;
    }

    private static View Slot(string iconData, string label, string route, bool isActive)
    {
        var colour = isActive ? Tokens.BrandAccent : Tokens.TabInactive;

        var icon = new Path
        {
            Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(iconData)!,
            Stroke = new SolidColorBrush(colour),
            StrokeThickness = 1.7,
            StrokeLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
            Fill = Colors.Transparent,
            Aspect = Stretch.Uniform,
            WidthRequest = 23,
            HeightRequest = 23,
            HorizontalOptions = LayoutOptions.Center
        };

        var stack = new VerticalStackLayout
        {
            Spacing = 3,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                icon,
                new Label
                {
                    Text = label,
                    FontSize = 11,
                    FontAttributes = isActive ? FontAttributes.Bold : FontAttributes.None,
                    TextColor = colour,
                    HorizontalTextAlignment = TextAlignment.Center
                }
            }
        };

        var target = new ContentView
        {
            MinimumHeightRequest = Tokens.MinimumTouchTarget,
            Content = stack
        };

        if (!isActive)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await Shell.Current.GoToAsync(route);
            target.GestureRecognizers.Add(tap);
        }

        SemanticProperties.SetDescription(target, isActive ? $"{label}, selected" : label);
        return target;
    }
}

/// <summary>One tile on the quick actions sheet.</summary>
public sealed record QuickAction(string Glyph, string Title, string Subtitle, Color Tint, Func<Task> OnTap);

/// <summary>
/// EW-006. The body of the sheet the coral (+) opens (EMP-063): a 2×2 grid of large tiles.
/// Actions HR has switched off are simply not passed in, and the grid reflows (build plan §3.2).
/// </summary>
public static class QuickActionsSheet
{
    public static View Build(IReadOnlyList<QuickAction> actions)
    {
        var grid = new Grid
        {
            ColumnSpacing = Tokens.SpaceCardGap,
            RowSpacing = Tokens.SpaceCardGap,
            Padding = new Thickness(Tokens.SpaceGutter, 8, Tokens.SpaceGutter, 28),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star)]
        };

        for (var row = 0; row < (actions.Count + 1) / 2; row++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        }

        for (var index = 0; index < actions.Count; index++)
        {
            grid.Add(Tile(actions[index]), index % 2, index / 2);
        }

        return grid;
    }

    private static View Tile(QuickAction action)
    {
        var tile = new Border
        {
            BackgroundColor = action.Tint,
            StrokeThickness = 0,
            Padding = 16,
            MinimumHeightRequest = 110,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusTile },
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    new Border
                    {
                        WidthRequest = 40,
                        HeightRequest = 40,
                        BackgroundColor = Colors.White,
                        StrokeThickness = 0,
                        HorizontalOptions = LayoutOptions.Start,
                        StrokeShape = new RoundRectangle { CornerRadius = 20 },
                        Content = new Label
                        {
                            Text = action.Glyph,
                            FontSize = 18,
                            HorizontalTextAlignment = TextAlignment.Center,
                            VerticalTextAlignment = TextAlignment.Center
                        }
                    },
                    new Label
                    {
                        Text = action.Title,
                        FontSize = 14,
                        FontAttributes = FontAttributes.Bold,
                        TextColor = Tokens.TextPrimary
                    },
                    new Label
                    {
                        Text = action.Subtitle,
                        FontSize = 11,
                        TextColor = Tokens.TextSecondary
                    }
                }
            }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await action.OnTap();
        tile.GestureRecognizers.Add(tap);

        SemanticProperties.SetDescription(tile, $"{action.Title}. {action.Subtitle}");
        return tile;
    }
}
