namespace Vispl.HRManagement.EmployeeApp.Shared.Theme;

/// <summary>
/// The design tokens from <c>docs/employee-app/widgets.md</c> §3 and
/// <c>docs/employee-app/employee-app-build-plan.md</c> §2.
///
/// Every value here was sampled from the approved mockups in <c>scopedocs/app2</c> rather than
/// chosen (re-sampled pixel by pixel on 2026-09-15). This class is the only place a literal colour
/// appears; controls take the semantic name, so a re-sample changes one file.
/// </summary>
public static class Tokens
{
    // ---- Brand and surface -------------------------------------------------

    /// <summary>Identity card (5), profile header (2), FAB and primary buttons (6, 7).</summary>
    public static Color BrandAccent => EmployeeTheme.Colour(EmployeeTheme.Current.Primary);
    public static Color BrandAccentSoft => EmployeeTheme.Lighten(EmployeeTheme.Current.Primary, .48);
    public static Color BrandAccentTint => EmployeeTheme.Lighten(EmployeeTheme.Current.Primary, .91);
    public static readonly Color OnAccent = Colors.White;

    public static readonly Color Background = Colors.White;
    public static readonly Color Surface = Colors.White;
    public static readonly Color SurfaceMuted = Color.FromArgb("#F6F6F6");
    public static readonly Color CalendarGround = Color.FromArgb("#F7F7F7");

    public static readonly Color TextPrimary = Color.FromArgb("#111827");
    public static readonly Color TextSecondary = Color.FromArgb("#6B7280");
    public static readonly Color TextTertiary = Color.FromArgb("#9CA3AF");
    public static readonly Color Border = Color.FromArgb("#EEEEEE");

    // ---- Chrome ------------------------------------------------------------

    /// <summary>Circular search and bell buttons on the app bar (5).</summary>
    public static readonly Color AppBarButton = Color.FromArgb("#F4F4F4");

    /// <summary>Bottom bar: active slot is the accent; this is every other slot (1, 5).</summary>
    public static readonly Color TabInactive = Color.FromArgb("#6B6B6B");

    /// <summary>Hairline above the bottom bar.</summary>
    public static readonly Color TabBarRule = Color.FromArgb("#EFEFEF");

    /// <summary>View Full Month, Download report (6, 7).</summary>
    public static readonly Color SecondaryButton = Color.FromArgb("#F0F0F0");

    /// <summary>The rounded square behind each Menu row's emoji (1).</summary>
    public static readonly Color MenuIconTile = Color.FromArgb("#F1F0F0");

    // ---- Home KPI tiles (5.png) -------------------------------------------
    // Tiles are coloured by grid position. Slot 3 and 4 carry Pending Requests and Gate Passes
    // (build plan D2) in the image's mint and peach.

    public static Color KpiRoseFrom => EmployeeTheme.Lighten(EmployeeTheme.Current.KpiOne, .78);
    public static Color KpiRoseTo => EmployeeTheme.Lighten(EmployeeTheme.Current.KpiOne, .86);
    public static Color KpiRoseIcon => EmployeeTheme.Colour(EmployeeTheme.Current.KpiOne);

    public static Color KpiPeriwinkleFrom => EmployeeTheme.Lighten(EmployeeTheme.Current.KpiTwo, .72);
    public static Color KpiPeriwinkleTo => EmployeeTheme.Lighten(EmployeeTheme.Current.KpiTwo, .84);
    public static Color KpiPeriwinkleIcon => EmployeeTheme.Colour(EmployeeTheme.Current.KpiTwo);

    public static Color KpiMintFrom => EmployeeTheme.Lighten(EmployeeTheme.Current.KpiThree, .72);
    public static Color KpiMintTo => EmployeeTheme.Lighten(EmployeeTheme.Current.KpiThree, .84);
    public static Color KpiMintIcon => EmployeeTheme.Colour(EmployeeTheme.Current.KpiThree);

    public static Color KpiPeachFrom => EmployeeTheme.Lighten(EmployeeTheme.Current.KpiFour, .70);
    public static Color KpiPeachTo => EmployeeTheme.Lighten(EmployeeTheme.Current.KpiFour, .84);
    public static Color KpiPeachIcon => EmployeeTheme.Colour(EmployeeTheme.Current.KpiFour);

    public static readonly Color KpiValueText = Color.FromArgb("#1F2937");
    public static readonly Color KpiLabelText = Color.FromArgb("#374151");

    // ---- Home interview card (5.png) --------------------------------------

    public static Color InterviewRowTint => EmployeeTheme.Colour(EmployeeTheme.Current.Card);
    public static Color InterviewRule => BrandAccent;
    public static readonly Color PastInterviewPill = Color.FromArgb("#FF8A80");
    public static readonly Color CountBadgeTint = Color.FromArgb("#FFE1DE");

    // ---- Sheets (2, 3) ----------------------------------------------------

    /// <summary>The name on the profile header card is a warm yellow, not white (2).</summary>
    public static readonly Color ProfileNameAccent = Color.FromArgb("#FFD37A");

    /// <summary>Payment Details plate on the salary sheet, with a coral left rule (3).</summary>
    public static readonly Color PaymentTermsTint = Color.FromArgb("#FCF1F0");

    // ---- Status palette ----------------------------------------------------
    // StatusDto.Tone and KpiDto.Tone are semantic strings from the server. This is the only
    // place a tone becomes a colour; never send a hex value from the API.

    public static readonly Color StatusSuccess = Color.FromArgb("#84D382");
    public static readonly Color StatusWarning = Color.FromArgb("#F6A549");
    public static readonly Color StatusPending = Color.FromArgb("#FBDD59");
    public static readonly Color StatusDanger = Color.FromArgb("#FF6153");
    public static readonly Color StatusInfo = Color.FromArgb("#779ADA");
    public static readonly Color StatusMuted = Color.FromArgb("#B8B8B8");

    /// <summary>Maps a server tone to its colour. An unknown tone renders muted, never invented.</summary>
    public static Color ForTone(string? tone) => (tone ?? string.Empty).ToLowerInvariant() switch
    {
        "success" => StatusSuccess,
        "warning" => StatusWarning,
        "pending" => StatusPending,
        "danger" or "error" => StatusDanger,
        "info" or "accent" => StatusInfo,
        _ => StatusMuted
    };

    /// <summary>Text that stays legible on a tone fill. The image's pills carry white text.</summary>
    public static Color OnTone(string? tone) => (tone ?? string.Empty).ToLowerInvariant() switch
    {
        "pending" => TextPrimary,
        _ => Colors.White
    };

    // ---- Attendance stat tiles (6.png) ------------------------------------

    public static readonly Color StatTileBorder = Color.FromArgb("#E5E5E5");
    public static readonly Color AttendancePresent = Color.FromArgb("#5EB465");
    public static readonly Color AttendanceAbsent = Color.FromArgb("#FF6153");
    public static readonly Color AttendanceHalfDay = Color.FromArgb("#F6A549");
    public static readonly Color AttendanceHoliday = Color.FromArgb("#6D9BE0");

    // ---- Attendance calendar cells (7.png) --------------------------------
    // The image's calendar has exactly four cell states and no weekend colour: the yellow cells
    // are half days (they match the Half Days tile count), not Saturdays.

    public static readonly Color CalPresent = Color.FromArgb("#9DD697");
    public static readonly Color CalHalfDay = Color.FromArgb("#F8E27E");
    public static readonly Color CalAbsent = Color.FromArgb("#EC968F");
    public static readonly Color CalHoliday = Color.FromArgb("#7397D9");
    public static readonly Color CalLeave = Color.FromArgb("#FBDD59");
    public static readonly Color CalFuture = Color.FromArgb("#F7F7F7");
    public static readonly Color CalWeekdayHeader = Color.FromArgb("#4A6FD1");

    /// <summary>
    /// Maps an <c>AttendanceDayDto.Status.Code</c> to its cell colour. "Partial" is what the
    /// mockups label Half Day — the app translates, the server does not rename.
    /// </summary>
    public static Color ForDayStatus(string? code) => (code ?? string.Empty).ToLowerInvariant() switch
    {
        "present" => CalPresent,
        "partial" => CalHalfDay,
        "absent" => CalAbsent,
        "holiday" => CalHoliday,
        "leave" => CalLeave,
        _ => CalFuture
    };

    // ---- Phase C: Attendance (6.png) --------------------------------------------
    // Chips sit under the row text, radius 3. Sampled from scopedocs/app2/6.png.

    public static readonly Color AttChipOnTime = Color.FromArgb("#84D382");
    public static readonly Color AttChipLate = Color.FromArgb("#FF6153");
    public static readonly Color AttChipHalf = Color.FromArgb("#F6A549");
    public static readonly Color AttChipLeave = Color.FromArgb("#FBDD59");
    public static readonly Color AttChipHoliday = Color.FromArgb("#6D9BE0");

    // ---- Phase C: Attendance Analytics (≈, read from the chat screenshots) --------

    public static readonly Color PeriodChipInactive = Color.FromArgb("#EEEEEE");
    public static readonly Color PeriodChipInactiveText = Color.FromArgb("#333333");

    public static readonly Color KpiBandOverallGround = Color.FromArgb("#FFFFFF");
    public static readonly Color KpiBandOverallBorder = Color.FromArgb("#D4D4D4");
    public static readonly Color KpiBandOverallInk = Color.FromArgb("#333333");
    public static readonly Color KpiBandPresentGround = Color.FromArgb("#EEF8F8");
    public static readonly Color KpiBandPresentBorder = Color.FromArgb("#3E8F6E");
    public static readonly Color KpiBandPresentInk = Color.FromArgb("#3D8B5F");
    public static readonly Color KpiBandAbsentGround = Color.FromArgb("#FDECEC");
    public static readonly Color KpiBandAbsentBorder = Color.FromArgb("#F2A7A7");
    public static readonly Color KpiBandAbsentInk = Color.FromArgb("#F07C80");
    public static readonly Color KpiBandHalfGround = Color.FromArgb("#FDF6E6");
    public static readonly Color KpiBandHalfBorder = Color.FromArgb("#F3BE66");
    public static readonly Color KpiBandHalfInk = Color.FromArgb("#F2A33C");

    public static readonly Color DeltaGood = Color.FromArgb("#4A9A5B");
    public static readonly Color DeltaBad = Color.FromArgb("#EE7F84");

    public static readonly Color InfoCardGround = Color.FromArgb("#EDF4FC");
    public static readonly Color InfoCardBorder = Color.FromArgb("#C9DCF0");
    public static readonly Color InfoDivider = Color.FromArgb("#D6E2F0");
    public static Color ChartBar => EmployeeTheme.Colour(EmployeeTheme.Current.Chart);
    public static Color WidgetCardTint => EmployeeTheme.Colour(EmployeeTheme.Current.Card);

    public static readonly Color StatsPlate = Color.FromArgb("#F4F4F4");
    public static readonly Color StatsDivider = Color.FromArgb("#E2E2E2");
    public static readonly Color StatsPresent = Color.FromArgb("#4A9A5B");
    public static readonly Color StatsAbsent = Color.FromArgb("#E0544B");
    public static readonly Color StatsHalf = Color.FromArgb("#F2A33C");

    // ---- Phase C: Leave Pending for Approval (≈) --------------------------------

    public static readonly Color ApprovalBannerGround = Color.FromArgb("#FDF3DF");
    public static readonly Color ApprovalBannerBorder = Color.FromArgb("#F1D7A2");
    public static readonly Color ApprovalBannerInk = Color.FromArgb("#A36B16");
    public static readonly Color ApprovalCardBorder = Color.FromArgb("#DDDDDD");
    public static readonly Color ReasonPlate = Color.FromArgb("#F2F2F2");
    public static readonly Color InfoBadge = Color.FromArgb("#2F6FD0");
    public static readonly Color DecisionApprove = Color.FromArgb("#5BA55E");
    public static readonly Color DecisionDisapprove = Color.FromArgb("#F4B04E");
    public static readonly Color DecisionCancel = Color.FromArgb("#8C8C8C");

    /// <summary>
    /// A leave type's colour family, from the tone key HR set on the policy. The HR portal's
    /// Leave Type Colours page shows the same pairs, so what HR picks is what employees see.
    /// </summary>
    public static (Color Strip, Color Ink) LeaveTone(string? toneKey) => (toneKey ?? string.Empty).ToLowerInvariant() switch
    {
        "pink" => (Color.FromArgb("#F5D5E0"), Color.FromArgb("#B8386A")),
        "green" => (Color.FromArgb("#DDEFDD"), Color.FromArgb("#3F8A46")),
        "purple" => (Color.FromArgb("#E6DDF5"), Color.FromArgb("#6B46B8")),
        "teal" => (Color.FromArgb("#D5EFEE"), Color.FromArgb("#1F8580")),
        "amber" => (Color.FromArgb("#FBEBD0"), Color.FromArgb("#B7791F")),
        _ => (Color.FromArgb("#D9E8F8"), Color.FromArgb("#2F6FD0"))
    };

    // ---- Money --------------------------------------------------------------

    /// <summary>Salary hero card (3).</summary>
    public static readonly Color MoneyPositive = Color.FromArgb("#78D17B");

    // ---- Shape, type and motion ---------------------------------------------

    public const int RadiusCard = 14;
    public const int RadiusSheet = 22;
    public const int RadiusPill = 999;
    public const int RadiusTile = 18;
    public const int RadiusCalendarCell = 8;

    public const int SpaceGutter = 16;
    public const int SpaceCardGap = 12;
    public const int SpaceSectionGap = 24;

    public const double FontSectionHeader = 12;
    public const double FontRowTitle = 15;
    public const double FontRowSubtitle = 12;
    public const double FontTileValue = 30;
    public const double FontTileLabel = 12;
    public const double FontSheetTitle = 16;

    /// <summary>The plan's accessibility floor: nothing smaller than this is tappable.</summary>
    public const double MinimumTouchTarget = 44;

    /// <summary>Height of the bottom bar itself, excluding the raised button above it.</summary>
    public const double TabBarHeight = 64;

    /// <summary>Diameter of the raised coral (+) button.</summary>
    public const double FabSize = 56;
}
