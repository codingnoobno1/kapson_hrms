using Vispl.HRManagement.Contracts.Mobile.Workforce;

namespace Vispl.HRManagement.UI.Components.Hr.Shared;

/// <summary>
/// The leave type colour families as the employee app draws them (Tokens.LeaveTone): a light strip
/// and a strong ink. The HR portal uses the same pairs so a Casual Leave tag on /hr/leave is the
/// same blue as the approval card on a manager's phone.
/// </summary>
public static class LeaveTonePalette
{
    private static readonly Dictionary<string, (string Strip, string Ink)> Pairs = new()
    {
        [LeaveTones.Blue] = ("#D9E8F8", "#2F6FD0"),
        [LeaveTones.Pink] = ("#F5D5E0", "#B8386A"),
        [LeaveTones.Green] = ("#DDEFDD", "#3F8A46"),
        [LeaveTones.Purple] = ("#E6DDF5", "#6B46B8"),
        [LeaveTones.Teal] = ("#D5EFEE", "#1F8580"),
        [LeaveTones.Amber] = ("#FBEBD0", "#B7791F")
    };

    public static string Strip(string? tone) => Pairs[LeaveTones.Normalize(tone)].Strip;

    public static string Ink(string? tone) => Pairs[LeaveTones.Normalize(tone)].Ink;

    /// <summary>Inline style for a tag in a leave type's colours.</summary>
    public static string TagStyle(string? tone) =>
        $"display:inline-flex;align-items:center;gap:5px;padding:3px 9px;border-radius:4px;font-weight:600;background:{Strip(tone)};color:{Ink(tone)}";
}
