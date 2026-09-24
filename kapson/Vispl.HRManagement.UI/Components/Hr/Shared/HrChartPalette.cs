namespace Vispl.HRManagement.UI.Components.Hr.Shared;

/// <summary>
/// The colours every chart draws from.
///
/// Held in one place so a leave type is the same colour in the donut, the grouped bars and the
/// stacked daily chart — a reader compares across widgets, and a policy that changes colour
/// between two panels on the same page is worse than no colour at all.
///
/// Status colours match the rest of the platform: green approved, amber pending, rose rejected,
/// the same palette the employee app uses for attendance.
/// </summary>
public static class HrChartPalette
{
    /// <summary>Categorical series, ordered for contrast between neighbours.</summary>
    public static readonly string[] Series =
    [
        "#2563eb", // blue
        "#7c3aed", // violet
        "#0891b2", // cyan
        "#d97706", // amber
        "#15803d", // green
        "#be123c", // rose
        "#4f46e5", // indigo
        "#a16207"  // ochre
    ];

    public const string Approved = "#16a34a";
    public const string Pending = "#d97706";
    public const string Rejected = "#dc2626";
    public const string Withdrawn = "#94a3b8";

    public const string Used = "#2563eb";
    public const string Available = "#bfdbfe";

    public const string Grid = "#e3e8f0";
    public const string Axis = "#94a3b8";

    /// <summary>
    /// A stable colour for a series. Keyed by position in the caller's own ordered list, so the
    /// same list always produces the same colours.
    /// </summary>
    public static string For(int index) => Series[index % Series.Length];
}
