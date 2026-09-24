using System.Text.RegularExpressions;

namespace Vispl.HRManagement.MobileApp.Services.Ui;

public sealed record HrThemePreset(
    string Key, string Name, string Description,
    string Primary, string PrimaryDeep, string KpiTwo, string KpiThree,
    string CardAccent, string ChartAccent);

/// <summary>Device-local appearance preferences. No tenant data or API call is involved.</summary>
public sealed partial class AppThemeService
{
    private const string Prefix = "vispl.hr.theme.";

    public static readonly IReadOnlyList<HrThemePreset> Presets =
    [
        new("reference", "Reference Blue", "The approved HR dashboard colours.", "#3478ED", "#2F68D5", "#12B7E8", "#FFC34E", "#EAF1FE", "#3478ED"),
        new("ocean", "Ocean", "Clear blue with teal reporting accents.", "#1677C8", "#0D5FA7", "#15B8A6", "#56C5E8", "#E7F4FA", "#15A6B8"),
        new("forest", "Forest", "Calm green with warm KPI contrast.", "#2E8B67", "#236E52", "#69B578", "#E2A83B", "#EAF6EF", "#3A9D7C"),
        new("violet", "Violet", "A polished purple and indigo palette.", "#7357D3", "#5940B4", "#4F7DE3", "#D991C3", "#F1EDFC", "#6C63D8"),
        new("sunset", "Sunset", "Warm coral balanced by amber and plum.", "#E96555", "#C94D41", "#F0A43A", "#A85D9E", "#FFF0EC", "#D96C58")
    ];

    public event Action? Changed;

    public string PresetKey { get; private set; } = "reference";
    public string Primary { get; private set; } = Presets[0].Primary;
    public string PrimaryDeep { get; private set; } = Presets[0].PrimaryDeep;
    public string KpiOne { get; private set; } = Presets[0].Primary;
    public string KpiTwo { get; private set; } = Presets[0].KpiTwo;
    public string KpiThree { get; private set; } = Presets[0].KpiThree;
    public string CardAccent { get; private set; } = Presets[0].CardAccent;
    public string ChartAccent { get; private set; } = Presets[0].ChartAccent;

    public AppThemeService() => Load();

    public string CssVariables => string.Join(';',
        $"--color-brand-primary:{Primary}",
        $"--color-brand-primary-deep:{PrimaryDeep}",
        $"--gradient-kpi-blue:linear-gradient(180deg,{KpiOne} 0%,{KpiOne} 100%)",
        $"--gradient-kpi-cyan:linear-gradient(180deg,{KpiTwo} 0%,{KpiTwo} 100%)",
        $"--gradient-kpi-amber:linear-gradient(180deg,{KpiThree} 0%,{KpiThree} 100%)",
        $"--color-widget-card-accent:{CardAccent}",
        $"--color-brand-tint:{CardAccent}",
        $"--color-widget-chart-accent:{ChartAccent}",
        $"--color-action-view:{Primary}",
        $"--elevation-fab:0 5px 14px {Primary}66");

    public void ApplyPreset(string key)
    {
        var preset = Presets.FirstOrDefault(item => item.Key == key) ?? Presets[0];
        PresetKey = preset.Key;
        Primary = preset.Primary;
        PrimaryDeep = preset.PrimaryDeep;
        KpiOne = preset.Primary;
        KpiTwo = preset.KpiTwo;
        KpiThree = preset.KpiThree;
        CardAccent = preset.CardAccent;
        ChartAccent = preset.ChartAccent;
        Save();
    }

    public bool SetWidgetColour(string widget, string colour)
    {
        if (!HexColour().IsMatch(colour)) return false;
        PresetKey = "custom";
        switch (widget)
        {
            case "primary": Primary = colour; PrimaryDeep = colour; break;
            case "kpi-one": KpiOne = colour; break;
            case "kpi-two": KpiTwo = colour; break;
            case "kpi-three": KpiThree = colour; break;
            case "card": CardAccent = colour; break;
            case "chart": ChartAccent = colour; break;
            default: return false;
        }
        Save();
        return true;
    }

    public void Reset() => ApplyPreset("reference");

    private void Load()
    {
        var savedPreset = Preferences.Default.Get(Prefix + "preset", "reference");
        var preset = Presets.FirstOrDefault(item => item.Key == savedPreset) ?? Presets[0];
        PresetKey = savedPreset;
        Primary = Read("primary", preset.Primary);
        PrimaryDeep = savedPreset == "custom" ? Primary : preset.PrimaryDeep;
        KpiOne = Read("kpi1", preset.Primary);
        KpiTwo = Read("kpi2", preset.KpiTwo);
        KpiThree = Read("kpi3", preset.KpiThree);
        CardAccent = Read("card", preset.CardAccent);
        ChartAccent = Read("chart", preset.ChartAccent);
    }

    private string Read(string key, string fallback)
    {
        var value = Preferences.Default.Get(Prefix + key, fallback);
        return HexColour().IsMatch(value) ? value : fallback;
    }

    private void Save()
    {
        Preferences.Default.Set(Prefix + "preset", PresetKey);
        Preferences.Default.Set(Prefix + "primary", Primary);
        Preferences.Default.Set(Prefix + "kpi1", KpiOne);
        Preferences.Default.Set(Prefix + "kpi2", KpiTwo);
        Preferences.Default.Set(Prefix + "kpi3", KpiThree);
        Preferences.Default.Set(Prefix + "card", CardAccent);
        Preferences.Default.Set(Prefix + "chart", ChartAccent);
        Changed?.Invoke();
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$", RegexOptions.CultureInvariant)]
    private static partial Regex HexColour();
}
