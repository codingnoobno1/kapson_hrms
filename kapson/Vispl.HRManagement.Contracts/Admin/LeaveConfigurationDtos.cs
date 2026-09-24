using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

/// <summary>
/// A leave policy as the admin panel edits it.
///
/// These are the rules the leave engine enforces when it computes chargeable days, so a change
/// here changes what future requests cost. Requests already submitted keep the day count that
/// was computed for them.
/// </summary>
public sealed class LeavePolicyAdminDto
{
    public long Id { get; set; }

    [Required, RegularExpression("^[a-z0-9-]+$")]
    public string TenantKey { get; set; } = string.Empty;

    [Required, RegularExpression("^[A-Z0-9_-]+$", ErrorMessage = "Use upper-case letters, digits, underscore or hyphen.")]
    [StringLength(40)]
    public string Code { get; set; } = string.Empty;

    /// <summary>Colour family on the employee app and the HR queue (LeaveTones: blue, pink, green…).</summary>
    [StringLength(20)]
    public string ToneKey { get; set; } = "blue";

    /// <summary>Icon beside the type in leave history and approval cards.</summary>
    [StringLength(16)]
    public string Emoji { get; set; } = "🗓️";

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string Unit { get; set; } = "Days";

    [Required, StringLength(20)]
    public string Version { get; set; } = "1";

    [Range(0, 400, ErrorMessage = "Entitlement must be between 0 and 400 days.")]
    public decimal AnnualEntitlement { get; set; }

    [Required, StringLength(20)]
    public string AccrualMode { get; set; } = "Annual";

    [Range(0, 400)]
    public decimal CarryForwardCap { get; set; }

    [Range(0, 365)]
    public int NoticeDays { get; set; }

    [Range(0, 400)]
    public decimal? DocumentRequiredAboveDays { get; set; }

    public bool AllowsHalfDay { get; set; } = true;
    public bool AllowsNegativeBalance { get; set; }
    public bool CountsNonWorkingDays { get; set; }

    [StringLength(2000)] public string EligibilityText { get; set; } = string.Empty;
    [StringLength(2000)] public string AccrualRuleText { get; set; } = string.Empty;
    [StringLength(2000)] public string CarryForwardRuleText { get; set; } = string.Empty;
    [StringLength(2000)] public string NoticeRuleText { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>Live requests on this policy. Shown so a rule is not changed blind.</summary>
    public int OpenRequestCount { get; set; }
}

/// <summary>A non-working day, excluded from chargeable leave.</summary>
public sealed class LeaveHolidayAdminDto
{
    public long Id { get; set; }

    [Required, RegularExpression("^[a-z0-9-]+$")]
    public string TenantKey { get; set; } = string.Empty;

    [Required]
    public DateOnly Date { get; set; }

    [Required, StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Blank applies the holiday everywhere; a code limits it to one location.</summary>
    [StringLength(40)]
    public string? LocationCode { get; set; }
}

/// <summary>The vocabulary the leave editor offers, so the UI does not invent its own.</summary>
public static class LeavePolicyOptions
{
    public static IReadOnlyList<string> Units { get; } = ["Days", "Hours"];

    public static IReadOnlyList<(string Code, string Description)> AccrualModes { get; } =
    [
        ("Annual", "Credited in full at the start of the year, pro-rated on joining."),
        ("Monthly", "Credited in instalments as each month of service completes."),
        ("None", "Not accrued. Granted per instance rather than per year.")
    ];
}
