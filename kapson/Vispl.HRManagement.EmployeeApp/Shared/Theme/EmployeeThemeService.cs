namespace Vispl.HRManagement.EmployeeApp.Shared.Theme;

public sealed record EmployeeThemePreset(
    string Key, string Name, string Description,
    string Primary, string KpiOne, string KpiTwo, string KpiThree, string KpiFour,
    string Card, string Chart);

public sealed record ThemeColourChoice(string Name, string Hex);

public static class EmployeeTheme
{
    public static EmployeeThemePreset Current { get; internal set; } = EmployeeThemeService.Presets[0];

    public static Color Colour(string hex) => Color.FromArgb(hex);

    public static Color Lighten(string hex, double amount) => Mix(hex, "#FFFFFF", amount);
    public static Color Darken(string hex, double amount) => Mix(hex, "#000000", amount);

    private static Color Mix(string from, string to, double amount)
    {
        var a = Color.FromArgb(from);
        var b = Color.FromArgb(to);
        var t = Math.Clamp(amount, 0, 1);
        return new Color(
            (float)(a.Red + (b.Red - a.Red) * t),
            (float)(a.Green + (b.Green - a.Green) * t),
            (float)(a.Blue + (b.Blue - a.Blue) * t));
    }
}

/// <summary>Internal, device-local employee-app theme selection.</summary>
public sealed class EmployeeThemeService
{
    private const string Prefix = "kapsons.employee.theme.";

    public static readonly IReadOnlyList<EmployeeThemePreset> Presets =
    [
        new("kapsons", "Kapsons Coral", "The approved employee app palette.", "#FF6153", "#F16B85", "#5669D8", "#42A875", "#E4A63A", "#FFF4F3", "#6D9BE0"),
        new("ocean", "Ocean", "Blue, teal and sea-glass widgets.", "#1877C9", "#2F88D8", "#27A7B8", "#36A47B", "#E3A437", "#EBF6FC", "#168FA8"),
        new("forest", "Forest", "Natural greens with warm highlights.", "#2E8B67", "#3A9D78", "#68A85B", "#91B84A", "#D19A35", "#ECF7F0", "#3B8F72"),
        new("violet", "Violet", "Purple, indigo and rose widgets.", "#7357D3", "#A45FC4", "#596FDB", "#8B68CA", "#D58C64", "#F3EFFD", "#7063D6"),
        new("sunrise", "Sunrise", "Coral, amber and berry accents.", "#E96555", "#E56B78", "#D98545", "#B6659A", "#E8AE3D", "#FFF1ED", "#D86C58")
    ];

    public static readonly IReadOnlyList<ThemeColourChoice> Colours =
    [
        new("Coral", "#FF6153"), new("Blue", "#3478ED"), new("Teal", "#15A6A0"),
        new("Green", "#3A9D6F"), new("Violet", "#7357D3"), new("Rose", "#D95F8D"),
        new("Amber", "#D99828"), new("Slate", "#52647A")
    ];

    public event Action? Changed;

    public EmployeeThemeService() => Load();

    public void ApplyPreset(string key)
    {
        EmployeeTheme.Current = Presets.FirstOrDefault(item => item.Key == key) ?? Presets[0];
        Save();
    }

    public void SetWidget(string widget, string hex)
    {
        if (!Colours.Any(item => item.Hex.Equals(hex, StringComparison.OrdinalIgnoreCase))) return;
        var p = EmployeeTheme.Current;
        EmployeeTheme.Current = widget switch
        {
            "primary" => p with { Key = "custom", Name = "Custom", Primary = hex },
            "kpi1" => p with { Key = "custom", Name = "Custom", KpiOne = hex },
            "kpi2" => p with { Key = "custom", Name = "Custom", KpiTwo = hex },
            "kpi3" => p with { Key = "custom", Name = "Custom", KpiThree = hex },
            "kpi4" => p with { Key = "custom", Name = "Custom", KpiFour = hex },
            "card" => p with { Key = "custom", Name = "Custom", Card = hex },
            "chart" => p with { Key = "custom", Name = "Custom", Chart = hex },
            _ => p
        };
        Save();
    }

    public void Reset() => ApplyPreset("kapsons");

    private void Load()
    {
        var key = Preferences.Default.Get(Prefix + "preset", "kapsons");
        var preset = Presets.FirstOrDefault(item => item.Key == key) ?? Presets[0];
        if (key == "custom")
        {
            preset = preset with
            {
                Key = "custom", Name = "Custom",
                Primary = Read("primary", preset.Primary), KpiOne = Read("kpi1", preset.KpiOne),
                KpiTwo = Read("kpi2", preset.KpiTwo), KpiThree = Read("kpi3", preset.KpiThree),
                KpiFour = Read("kpi4", preset.KpiFour), Card = Read("card", preset.Card),
                Chart = Read("chart", preset.Chart)
            };
        }
        EmployeeTheme.Current = preset;
    }

    private string Read(string key, string fallback)
    {
        var saved = Preferences.Default.Get(Prefix + key, fallback);
        return Colours.Any(item => item.Hex.Equals(saved, StringComparison.OrdinalIgnoreCase)) ? saved : fallback;
    }

    private void Save()
    {
        var p = EmployeeTheme.Current;
        Preferences.Default.Set(Prefix + "preset", p.Key);
        Preferences.Default.Set(Prefix + "primary", p.Primary);
        Preferences.Default.Set(Prefix + "kpi1", p.KpiOne);
        Preferences.Default.Set(Prefix + "kpi2", p.KpiTwo);
        Preferences.Default.Set(Prefix + "kpi3", p.KpiThree);
        Preferences.Default.Set(Prefix + "kpi4", p.KpiFour);
        Preferences.Default.Set(Prefix + "card", p.Card);
        Preferences.Default.Set(Prefix + "chart", p.Chart);
        Changed?.Invoke();
    }
}
