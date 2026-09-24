using Syncfusion.Maui.Toolkit.BottomSheet;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;

namespace Vispl.HRManagement.EmployeeApp.Shared.Sheets;

/// <summary>
/// EW-010 to EW-014. The app's primary detail surface.
///
/// The mockups open details as a rounded bottom sheet with a title row and an X, not as a
/// pushed page — nineteen of the plan's screens are sheets. MAUI has no such control, so this
/// wraps Syncfusion's <see cref="SfBottomSheet"/>, which brings the parts that are tedious to
/// get right by hand: snap points, drag-to-dismiss, a scrim that closes on tap, and a grabber.
///
/// A page hosts one of these and pushes content into it. Keeping the host on the page rather
/// than creating one per sheet means the scrim and the animation are shared, and two sheets
/// cannot end up stacked on each other.
/// </summary>
public sealed class AppSheet : SfBottomSheet
{
    private readonly Label titleLabel = new()
    {
        FontSize = Tokens.FontSheetTitle,
        FontAttributes = FontAttributes.Bold,
        TextColor = Tokens.TextPrimary,
        VerticalTextAlignment = TextAlignment.Center
    };

    private readonly ContentView body = new();

    public AppSheet()
    {
        CornerRadius = new CornerRadius(Tokens.RadiusSheet, Tokens.RadiusSheet, 0, 0);
        ContentPadding = 0;
        ShowGrabber = true;
        EnableSwiping = true;
        CollapseOnOverlayTap = true;
        IsModal = true;

        // Half height is the resting state; a long sheet is dragged to full rather than opening
        // over the whole screen, so the person keeps their place on the page underneath.
        AllowedState = BottomSheetAllowedState.All;
        HalfExpandedRatio = 0.62;
        FullExpandedRatio = 0.94;

        var close = new Border
        {
            WidthRequest = Tokens.MinimumTouchTarget,
            HeightRequest = Tokens.MinimumTouchTarget,
            BackgroundColor = Colors.Transparent,
            StrokeThickness = 0,
            HorizontalOptions = LayoutOptions.End,
            Content = new Label
            {
                Text = "✕",
                FontSize = 16,
                TextColor = Tokens.TextSecondary,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center
            }
        };

        var closeTap = new TapGestureRecognizer();
        closeTap.Tapped += (_, _) => Hide();
        close.GestureRecognizers.Add(closeTap);
        SemanticProperties.SetDescription(close, "Close");

        var header = new Grid
        {
            Padding = new Thickness(Tokens.SpaceGutter, 4, 8, 8),
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)]
        };

        header.Add(titleLabel, 0);
        header.Add(close, 1);

        var hairline = new BoxView
        {
            HeightRequest = 1,
            Color = Tokens.Border,
            Margin = new Thickness(0, 0, 0, 4)
        };

        // One scroll region for the whole sheet. Nesting a second one is the fastest way to
        // make a sheet impossible to drag closed.
        var scroller = new ScrollView { Content = body };

        BottomSheetContent = new Grid
        {
            BackgroundColor = Tokens.Surface,
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Star)
            ],
            Children =
            {
                Row(header, 0),
                Row(hairline, 1),
                Row(scroller, 2)
            }
        };
    }

    /// <summary>Opens the sheet on a title and a body. Replacing content closes nothing.</summary>
    public void Present(string title, View content, bool fullHeight = false)
    {
        titleLabel.Text = title;
        body.Content = content;

        SemanticProperties.SetHeadingLevel(titleLabel, SemanticHeadingLevel.Level1);
        SemanticProperties.SetDescription(this, title);

        // State first, then Show(): the base Show() is parameterless, so the resting height has
        // to be set before it animates in or the sheet opens at the wrong snap point.
        State = fullHeight ? BottomSheetState.FullExpanded : BottomSheetState.HalfExpanded;
        Show();
    }

    public void Hide() => Close();

    private static View Row(View view, int row)
    {
        Grid.SetRow(view, row);
        return view;
    }
}

/// <summary>
/// The parts a sheet's body is assembled from: EW-070 <c>FieldRow</c>, EW-071 <c>FieldCard</c>.
///
/// Facts inside a sheet are label-over-value rows on a muted plate, never a two-column table —
/// a table cannot survive a long value or large text.
/// </summary>
public static class SheetContent
{
    public static View FieldCard(params (string Label, string? Value)[] fields)
    {
        var stack = new VerticalStackLayout { Spacing = 0 };
        var rendered = fields.Where(field => !string.IsNullOrWhiteSpace(field.Value)).ToList();

        for (var index = 0; index < rendered.Count; index++)
        {
            stack.Add(FieldRow(rendered[index].Label, rendered[index].Value!));

            if (index < rendered.Count - 1)
            {
                stack.Add(new BoxView { HeightRequest = 1, Color = Tokens.Border });
            }
        }

        return new Border
        {
            BackgroundColor = Tokens.SurfaceMuted,
            StrokeThickness = 0,
            Margin = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, Tokens.SpaceCardGap),
            StrokeShape = new RoundRectangle { CornerRadius = Tokens.RadiusCard },
            Content = stack
        };
    }

    public static View FieldRow(string label, string value) => new VerticalStackLayout
    {
        Spacing = 2,
        Padding = new Thickness(14, 12),
        Children =
        {
            new Label { Text = label, FontSize = 11, TextColor = Tokens.TextSecondary },
            new Label
            {
                Text = value,
                FontSize = 14,
                TextColor = Tokens.TextPrimary
            }
        }
    };

    public static View SectionHeader(string text) => new Label
    {
        Text = text.ToUpperInvariant(),
        FontSize = Tokens.FontSectionHeader,
        FontAttributes = FontAttributes.Bold,
        CharacterSpacing = 1,
        TextColor = Tokens.TextSecondary,
        Margin = new Thickness(Tokens.SpaceGutter, 8, Tokens.SpaceGutter, 8)
    };

    /// <summary>EW-103 inside a sheet, for a row whose module answers 501.</summary>
    public static View NotReady(string feature) => new VerticalStackLayout
    {
        Spacing = 8,
        Padding = 32,
        Children =
        {
            new Label { Text = "\U0001F6A7", FontSize = 30, HorizontalTextAlignment = TextAlignment.Center },
            new Label
            {
                Text = $"{feature} is not available yet",
                FontSize = 16,
                FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = Tokens.TextPrimary
            },
            new Label
            {
                Text = "It has not been built. Nothing is wrong with your account.",
                FontSize = 13,
                HorizontalTextAlignment = TextAlignment.Center,
                TextColor = Tokens.TextSecondary
            }
        }
    };
}
