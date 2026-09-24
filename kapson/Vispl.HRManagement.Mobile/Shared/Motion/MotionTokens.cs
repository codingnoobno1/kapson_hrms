namespace Vispl.HRManagement.Mobile.Shared.Motion;

/// <summary>
/// The durations and distances named in scopedocs/screen-references/hr-mobile-visual-map.md
/// §"Motion mapping". One place so a control never has to guess whether "the button press
/// timing" means 120 or 150.
/// </summary>
public static class MotionTokens
{
    /// <summary>Button/action press feedback.</summary>
    public const uint DurationPress = 120;

    /// <summary>Page/list arrival, segmented selection, bottom sheet and drawer's fast edge.</summary>
    public const uint DurationBase = 180;

    /// <summary>KPI tiles, center FAB, bottom sheet/drawer's slow edge, analytics fill.</summary>
    public const uint DurationEmphasis = 260;

    /// <summary>One shimmer cycle on a loading skeleton.</summary>
    public const uint DurationShimmer = 1400;

    /// <summary>How far an arriving element rises while it fades in.</summary>
    public const double RiseDistance = 10;

    /// <summary>Gap between successive items in a hand-rolled (non-virtualised) list's stagger.</summary>
    public const uint StaggerStep = 40;

    /// <summary>
    /// Items past this position in a staggered list start at the same delay as this one, so a
    /// fifty-row list does not make the fiftieth row wait two seconds to appear.
    /// </summary>
    public const int StaggerCap = 8;

    /// <summary>How far a press dips a button/tile — a fraction of full size, not a pixel count.</summary>
    public const double PressScale = 0.96;

    public static readonly Easing EntranceEasing = Easing.CubicOut;
    public static readonly Easing PressEasing = Easing.CubicOut;
    public static readonly Easing SettleEasing = Easing.SpringOut;
}
