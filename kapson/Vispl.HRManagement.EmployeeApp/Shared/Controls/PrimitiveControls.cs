using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace Vispl.HRManagement.EmployeeApp.Shared.Controls;

/// <summary>
/// Line icons as path data, drawn in a 24×24 box. They tint with state the way the image's icons
/// do; emoji cannot be recoloured and the app ships no icon font.
/// </summary>
public static class Icons
{
    public const string Calendar = "M4,6 L20,6 L20,20 L4,20 Z M4,10.5 L20,10.5 M8,3 L8,7.5 M16,3 L16,7.5";
    public const string BarChart = "M5,20 L5,12 M10,20 L10,6 M15,20 L15,14 M20,20 L20,9";
    public const string ClipboardCheck = "M9,3.5 L15,3.5 L15,6.5 L9,6.5 Z M7,5 L5,5 L5,21 L19,21 L19,5 L17,5 M8.5,13.5 L11,16 L15.5,11.5";
    public const string Ticket = "M3.5,7 L20.5,7 L20.5,10.2 A1.8,1.8 0 0 0 20.5,13.8 L20.5,17 L3.5,17 L3.5,13.8 A1.8,1.8 0 0 0 3.5,10.2 Z M14,7 L14,17";
    public const string CheckCircle = "M12,3 A9,9 0 1 1 11.99,3 Z M7.8,12.4 L10.6,15.2 L16.2,9.6";
    public const string CrossCircle = "M12,3 A9,9 0 1 1 11.99,3 Z M9,9 L15,15 M15,9 L9,15";
    public const string Hourglass = "M7,3 L17,3 M7,21 L17,21 M8.5,3 C8.5,9 15.5,9 15.5,12 C15.5,15 8.5,15 8.5,21 M15.5,3 C15.5,9 8.5,9 8.5,12 C8.5,15 15.5,15 15.5,21";
    public const string Chat = "M4,5 L20,5 L20,15.5 L13,15.5 L9,19.5 L9,15.5 L4,15.5 Z";
    public const string Clock = "M12,3 A9,9 0 1 1 11.99,3 Z M12,7 L12,12 L15.5,14";

    public static Path Create(string data, Color colour, double size = 22, double thickness = 1.8) => new()
    {
        Data = (Geometry)new PathGeometryConverter().ConvertFromInvariantString(data)!,
        Stroke = new SolidColorBrush(colour),
        StrokeThickness = thickness,
        StrokeLineCap = PenLineCap.Round,
        StrokeLineJoin = PenLineJoin.Round,
        Fill = Colors.Transparent,
        Aspect = Stretch.Uniform,
        WidthRequest = size,
        HeightRequest = size,
        HorizontalOptions = LayoutOptions.Center,
        VerticalOptions = LayoutOptions.Center
    };
}

/// <summary>
/// EW-042. Status is always a filled pill, coloured from the fixed palette.
///
/// The colour never carries the meaning on its own: <c>StatusDto.Name</c> is the label and the
/// accessible name, so the pill reads correctly to someone who cannot distinguish the fill.
/// </summary>
public sealed class StatusPill : ContentView
{
    private readonly Label label = new()
    {
        FontSize = 11,
        FontAttributes = FontAttributes.Bold,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    private readonly Border border;

    public StatusPill()
    {
        border = new Border
        {
            StrokeThickness = 0,
            Padding = new Thickness(10, 4),
            HorizontalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusPill },
            Content = label
        };
        Motion.AddDepth(border, .055f);

        Content = border;
    }

    public static readonly BindableProperty StatusProperty = BindableProperty.Create(
        nameof(Status), typeof(StatusDto), typeof(StatusPill), propertyChanged: OnStatusChanged);

    public StatusDto? Status
    {
        get => (StatusDto?)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    private static void OnStatusChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var pill = (StatusPill)bindable;
        var status = newValue as StatusDto;

        pill.IsVisible = status is not null;

        if (status is null)
        {
            return;
        }

        pill.label.Text = status.Name;
        pill.label.TextColor = Tokens.OnTone(status.Tone);
        pill.border.BackgroundColor = Tokens.ForTone(status.Tone);
        SemanticProperties.SetDescription(pill, status.Name);
    }
}

/// <summary>
/// EW-033 and EW-035. A Home tile as 5.png draws it: pastel gradient, white icon chip with a
/// tinted line icon, the label, then the big value.
///
/// Colour follows grid position (rose, periwinkle, mint, peach); the icon follows the tile's key.
/// A <see cref="KpiDto"/> carrying an <c>UnavailableReason</c> renders an em dash — never a number
/// nobody stands behind.
/// </summary>
public sealed class KpiTile : ContentView
{
    private readonly Label valueLabel = new()
    {
        FontSize = Tokens.FontTileValue,
        TextColor = Tokens.KpiValueText
    };

    private readonly Label titleLabel = new()
    {
        FontSize = 13,
        TextColor = Tokens.KpiLabelText
    };

    private readonly Label unitLabel = new()
    {
        FontSize = Tokens.FontTileLabel,
        TextColor = Tokens.KpiLabelText,
        VerticalOptions = LayoutOptions.End,
        Margin = new Thickness(4, 0, 0, 7)
    };

    private readonly Border chip;
    private readonly Border border;

    public KpiTile()
    {
        chip = new Border
        {
            WidthRequest = 38,
            HeightRequest = 38,
            BackgroundColor = Colors.White,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = 19 }
        };

        border = new Border
        {
            StrokeThickness = 0,
            Padding = new Thickness(16, 16, 16, 14),
            MinimumHeightRequest = 124,
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusTile },
            Content = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    chip,
                    titleLabel,
                    new HorizontalStackLayout { Children = { valueLabel, unitLabel } }
                }
            }
        };

        Content = border;
    }

    public static readonly BindableProperty KpiProperty = BindableProperty.Create(
        nameof(Kpi), typeof(KpiDto), typeof(KpiTile), propertyChanged: OnKpiChanged);

    /// <summary>Position in the grid, which picks the gradient.</summary>
    public static readonly BindableProperty SlotProperty = BindableProperty.Create(
        nameof(Slot), typeof(int), typeof(KpiTile), 0, propertyChanged: OnKpiChanged);

    public KpiDto? Kpi
    {
        get => (KpiDto?)GetValue(KpiProperty);
        set => SetValue(KpiProperty, value);
    }

    public int Slot
    {
        get => (int)GetValue(SlotProperty);
        set => SetValue(SlotProperty, value);
    }

    private static void OnKpiChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var tile = (KpiTile)bindable;
        var kpi = tile.Kpi;

        var (from, to, iconColour) = tile.Slot switch
        {
            0 => (Tokens.KpiRoseFrom, Tokens.KpiRoseTo, Tokens.KpiRoseIcon),
            1 => (Tokens.KpiPeriwinkleFrom, Tokens.KpiPeriwinkleTo, Tokens.KpiPeriwinkleIcon),
            2 => (Tokens.KpiMintFrom, Tokens.KpiMintTo, Tokens.KpiMintIcon),
            _ => (Tokens.KpiPeachFrom, Tokens.KpiPeachTo, Tokens.KpiPeachIcon)
        };

        tile.border.Background = new LinearGradientBrush(
            [new GradientStop(from, 0f), new GradientStop(to, 1f)],
            new Point(0, 0),
            new Point(1, 1));

        if (kpi is null)
        {
            tile.IsVisible = false;
            return;
        }

        var icon = kpi.Key switch
        {
            EmployeeKpiKeys.PresentDays => Icons.Calendar,
            EmployeeKpiKeys.LeaveBalance => Icons.BarChart,
            EmployeeKpiKeys.PendingRequests => Icons.ClipboardCheck,
            EmployeeKpiKeys.GatePasses => Icons.Ticket,
            _ => Icons.CheckCircle
        };

        tile.chip.Content = Icons.Create(icon, iconColour, 20, 2);
        tile.IsVisible = true;
        tile.titleLabel.Text = kpi.Title;

        var unavailable = !string.IsNullOrWhiteSpace(kpi.UnavailableReason);

        tile.valueLabel.Text = unavailable ? "—" : kpi.Value;
        tile.valueLabel.TextColor = unavailable ? Tokens.StatusMuted : Tokens.KpiValueText;

        tile.unitLabel.Text = kpi.Unit ?? string.Empty;
        tile.unitLabel.IsVisible = !unavailable && !string.IsNullOrWhiteSpace(kpi.Unit);

        SemanticProperties.SetDescription(
            tile,
            unavailable
                ? $"{kpi.Title}. {kpi.UnavailableReason}"
                : $"{kpi.Title}, {kpi.Value} {kpi.Unit}".TrimEnd());
    }
}

/// <summary>
/// EW-040. An Attendance stat as 6.png draws it: white card with a grey hairline, a coloured
/// status icon, the value in the same colour, and a grey label.
///
/// The colour is fixed per tile key — green present, coral absent, amber half day, blue holiday —
/// exactly as the image pairs them.
/// </summary>
public sealed class StatTile : ContentView
{
    private readonly Label valueLabel = new()
    {
        FontSize = 26,
        FontAttributes = FontAttributes.Bold,
        HorizontalTextAlignment = TextAlignment.Center
    };

    private readonly Label titleLabel = new()
    {
        FontSize = Tokens.FontTileLabel,
        TextColor = Tokens.TextSecondary,
        HorizontalTextAlignment = TextAlignment.Center
    };

    private readonly ContentView iconHost = new() { HorizontalOptions = LayoutOptions.Center };

    public StatTile()
    {
        var card = new Border
        {
            Stroke = Tokens.StatTileBorder,
            StrokeThickness = 1,
            BackgroundColor = Tokens.Surface,
            Padding = new Thickness(12, 14),
            MinimumHeightRequest = 100,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            Content = new VerticalStackLayout
            {
                Spacing = 2,
                VerticalOptions = LayoutOptions.Center,
                Children = { iconHost, valueLabel, titleLabel }
            }
        };
        Motion.AddDepth(card, .045f);
        Content = card;
    }

    public static readonly BindableProperty KpiProperty = BindableProperty.Create(
        nameof(Kpi), typeof(KpiDto), typeof(StatTile), propertyChanged: OnChanged);

    /// <summary>Kept for callers that set it; the icon is chosen from the tile key.</summary>
    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
        nameof(Glyph), typeof(string), typeof(StatTile), "•", propertyChanged: OnChanged);

    public KpiDto? Kpi
    {
        get => (KpiDto?)GetValue(KpiProperty);
        set => SetValue(KpiProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var tile = (StatTile)bindable;
        var kpi = tile.Kpi;

        if (kpi is null)
        {
            tile.IsVisible = false;
            return;
        }

        var (icon, colour) = kpi.Key switch
        {
            EmployeeKpiKeys.PresentDays => (Icons.CheckCircle, Tokens.AttendancePresent),
            EmployeeKpiKeys.AbsentDays => (Icons.CrossCircle, Tokens.AttendanceAbsent),
            EmployeeKpiKeys.HalfDays => (Icons.Hourglass, Tokens.AttendanceHalfDay),
            EmployeeKpiKeys.Holidays => (Icons.Calendar, Tokens.AttendanceHoliday),
            _ => (Icons.CheckCircle, Tokens.ForTone(kpi.Tone))
        };

        var unavailable = !string.IsNullOrWhiteSpace(kpi.UnavailableReason);

        tile.IsVisible = true;
        tile.iconHost.Content = Icons.Create(icon, colour, 24, 2);
        tile.valueLabel.Text = unavailable ? "—" : kpi.Value;
        tile.valueLabel.TextColor = unavailable ? Tokens.StatusMuted : colour;
        tile.titleLabel.Text = kpi.Title;

        SemanticProperties.SetDescription(tile, $"{kpi.Title}, {tile.valueLabel.Text}");
    }
}
