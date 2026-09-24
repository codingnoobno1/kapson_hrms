using Microsoft.Maui.Controls.Shapes;
using Vispl.HRManagement.EmployeeApp.Features.Attendance;
using Vispl.HRManagement.EmployeeApp.Shared.Controls;
using Vispl.HRManagement.EmployeeApp.Shared.Theme;

namespace Vispl.HRManagement.EmployeeApp.Features.Menu;

/// <summary>Device-local appearance editor. It never sends appearance choices to the HR API.</summary>
public sealed class ThemeSettingsPage : ContentPage
{
    private readonly EmployeeThemeService themes;
    private readonly HorizontalStackLayout preview = new() { Spacing = 8 };
    private readonly Label current = new();

    public ThemeSettingsPage(EmployeeThemeService themes)
    {
        this.themes = themes;
        Shell.SetNavBarIsVisible(this, false);
        Shell.SetTabBarIsVisible(this, false);
        BackgroundColor = Tokens.Background;
        Title = "Appearance";

        current.FontSize = 12;
        current.TextColor = Tokens.TextSecondary;

        var stack = new VerticalStackLayout { Spacing = 12, Padding = new Thickness(Tokens.SpaceGutter, 0, Tokens.SpaceGutter, 28) };
        stack.Add(SectionTitle("APP THEME"));
        stack.Add(current);

        foreach (var preset in EmployeeThemeService.Presets)
        {
            stack.Add(PresetCard(preset));
        }

        stack.Add(SectionTitle("WIDGET COLOURS"));
        stack.Add(PickerRow("Brand & actions", "primary", () => EmployeeTheme.Current.Primary));
        stack.Add(PickerRow("KPI widget 1", "kpi1", () => EmployeeTheme.Current.KpiOne));
        stack.Add(PickerRow("KPI widget 2", "kpi2", () => EmployeeTheme.Current.KpiTwo));
        stack.Add(PickerRow("KPI widget 3", "kpi3", () => EmployeeTheme.Current.KpiThree));
        stack.Add(PickerRow("KPI widget 4", "kpi4", () => EmployeeTheme.Current.KpiFour));
        stack.Add(PickerRow("Detail cards", "card", () => EmployeeTheme.Current.Card));
        stack.Add(PickerRow("Charts & reports", "chart", () => EmployeeTheme.Current.Chart));

        stack.Add(SectionTitle("PREVIEW"));
        stack.Add(preview);

        var apply = ActionButton("Apply and return home", Tokens.BrandAccent, Colors.White, ApplyAsync);
        var reset = ActionButton("Restore Kapsons palette", Tokens.SecondaryButton, Tokens.TextPrimary, ResetAsync);
        stack.Add(apply);
        stack.Add(reset);

        Content = new Grid
        {
            RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
            Children =
            {
                Row(new BrandAppBar(), 0),
                Row(AttendanceVisuals.BackHeader("Appearance", () => Shell.Current.GoToAsync("..")), 1),
                Row(new ScrollView { Content = stack }, 2)
            }
        };

        RenderPreview();
    }

    private View PresetCard(EmployeeThemePreset preset)
    {
        var swatches = new HorizontalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center };
        foreach (var hex in new[] { preset.Primary, preset.KpiOne, preset.KpiTwo, preset.KpiThree, preset.KpiFour })
        {
            swatches.Add(new BoxView { WidthRequest = 13, HeightRequest = 36, Color = Color.FromArgb(hex) });
        }

        var card = new Border
        {
            Stroke = Tokens.Border,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Tokens.Surface,
            Padding = 12,
            Content = new Grid
            {
                ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
                ColumnSpacing = 12,
                Children =
                {
                    Col(swatches, 0),
                    Col(new VerticalStackLayout
                    {
                        Spacing = 2,
                        Children =
                        {
                            new Label { Text = preset.Name, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary },
                            new Label { Text = preset.Description, FontSize = 11, TextColor = Tokens.TextSecondary }
                        }
                    }, 1)
                }
            }
        };
        Motion.AddDepth(card, .04f);
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            await Motion.PulseAsync(card);
            themes.ApplyPreset(preset.Key);
            RenderPreview();
        };
        card.GestureRecognizers.Add(tap);
        return card;
    }

    private View PickerRow(string label, string widget, Func<string> selected)
    {
        var picker = new Picker
        {
            Title = "Choose",
            FontSize = 13,
            TextColor = Tokens.TextPrimary,
            HorizontalOptions = LayoutOptions.End,
            WidthRequest = 125,
            ItemsSource = EmployeeThemeService.Colours.Select(item => item.Name).ToList()
        };
        picker.SelectedIndex = EmployeeThemeService.Colours.ToList().FindIndex(item =>
            item.Hex.Equals(selected(), StringComparison.OrdinalIgnoreCase));
        picker.SelectedIndexChanged += (_, _) =>
        {
            if (picker.SelectedIndex < 0) return;
            themes.SetWidget(widget, EmployeeThemeService.Colours[picker.SelectedIndex].Hex);
            RenderPreview();
        };

        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto)],
            Padding = new Thickness(12, 5),
            BackgroundColor = Tokens.SurfaceMuted
        };
        grid.Add(new Label { Text = label, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Tokens.TextPrimary, VerticalOptions = LayoutOptions.Center }, 0);
        grid.Add(picker, 1);
        return new Border { StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 10 }, Content = grid };
    }

    private void RenderPreview()
    {
        var p = EmployeeTheme.Current;
        current.Text = p.Key == "custom" ? "Custom palette" : $"Selected: {p.Name}";
        preview.Clear();
        foreach (var (name, hex) in new[]
                 { ("Brand", p.Primary), ("KPI 1", p.KpiOne), ("KPI 2", p.KpiTwo), ("KPI 3", p.KpiThree), ("KPI 4", p.KpiFour), ("Chart", p.Chart) })
        {
            preview.Add(new VerticalStackLayout
            {
                Spacing = 4,
                HorizontalOptions = LayoutOptions.Center,
                Children =
                {
                    new Border { WidthRequest = 34, HeightRequest = 34, BackgroundColor = Color.FromArgb(hex), StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 17 } },
                    new Label { Text = name, FontSize = 9, TextColor = Tokens.TextSecondary, HorizontalTextAlignment = TextAlignment.Center }
                }
            });
        }
    }

    private Task ApplyAsync()
    {
        Application.Current!.Windows[0].Page = App.Services.GetRequiredService<AppShell>();
        return Task.CompletedTask;
    }

    private Task ResetAsync()
    {
        themes.Reset();
        RenderPreview();
        return Task.CompletedTask;
    }

    private static Label SectionTitle(string text) => new()
    {
        Text = text, FontSize = 12, FontAttributes = FontAttributes.Bold,
        CharacterSpacing = .5, TextColor = Tokens.TextSecondary, Margin = new Thickness(0, 12, 0, 0)
    };

    private static Button ActionButton(string text, Color ground, Color ink, Func<Task> action)
    {
        var button = new Button { Text = text, BackgroundColor = ground, TextColor = ink, CornerRadius = 12, HeightRequest = 48 };
        button.Clicked += async (_, _) => await action();
        return button;
    }

    private static View Row(View view, int row) { Grid.SetRow(view, row); return view; }
    private static View Col(View view, int column) { Grid.SetColumn(view, column); return view; }
}
