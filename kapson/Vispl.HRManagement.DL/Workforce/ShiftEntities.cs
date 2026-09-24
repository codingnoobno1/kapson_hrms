namespace Vispl.HRManagement.DL.Workforce;

/// <summary>
/// A named working pattern: when the day starts, how long it runs, and how much lateness is
/// tolerated before it counts.
///
/// Grace lives on the shift rather than as one global constant, because a retail floor shift
/// and a back-office shift do not tolerate the same slippage.
/// </summary>
public sealed class ShiftDefinitionEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    /// <summary>Unpaid break subtracted from the span to give expected working minutes.</summary>
    public int BreakMinutes { get; set; }

    /// <summary>Minutes after the start before an arrival counts as late.</summary>
    public int GraceMinutes { get; set; } = 15;

    /// <summary>
    /// Working days as a seven-character mask, Sunday first: <c>NYYYYYN</c> is Monday to Friday.
    /// A mask rather than a flags enum so it reads correctly in the database.
    /// </summary>
    public string WorkingDays { get; set; } = "NYYYYYN";

    /// <summary>
    /// True when the shift crosses midnight. Kept explicit rather than inferred from
    /// end &lt; start, so a badly entered shift is a validation failure, not a night shift.
    /// </summary>
    public bool CrossesMidnight { get; set; }

    /// <summary>The shift used for anyone with no assignment. Exactly one per tenant.</summary>
    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>
/// Which shift an employee works, over a period.
///
/// Assignments are dated ranges rather than one row per day: a person on the general shift for
/// two years is one row, and a week of nights is another. Per-day rows would be a hundred
/// thousand records saying the same thing.
/// </summary>
public sealed class ShiftAssignmentEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public long ShiftDefinitionId { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Null means open-ended — the current assignment.</summary>
    public DateOnly? EffectiveTo { get; set; }

    /// <summary>
    /// A later assignment wins where ranges overlap. Set from the effective date so a
    /// correction made today supersedes a roster entered last month.
    /// </summary>
    public DateTimeOffset AssignedUtc { get; set; }

    public string? AssignedBy { get; set; }
    public string? Note { get; set; }
}
