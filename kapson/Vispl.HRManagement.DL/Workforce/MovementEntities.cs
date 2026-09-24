namespace Vispl.HRManagement.DL.Workforce;

/// <summary>
/// One instance of an employee leaving the premises during working hours and coming back.
///
/// Personal and official are kept apart because they are not the same thing: official time out
/// is working time, personal time out may not be. Which it is has to be declared when the person
/// leaves, not decided afterwards when the payroll consequence is already known.
/// </summary>
public sealed class MovementRecordEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }

    /// <summary>Personal or Official.</summary>
    public string Kind { get; set; } = "Personal";

    public DateOnly WorkDate { get; set; }
    public DateTimeOffset GoingOutUtc { get; set; }
    public DateTimeOffset ExpectedReturnUtc { get; set; }

    /// <summary>Null while the person is still out.</summary>
    public DateTimeOffset? ActualReturnUtc { get; set; }

    /// <summary>Reason for personal absence, purpose for official.</summary>
    public string Detail { get; set; } = string.Empty;

    public string? DestinationLabel { get; set; }

    /// <summary>Open, Returned or Cancelled.</summary>
    public string State { get; set; } = "Open";

    public string? RecordedBy { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>
/// Permission to be off site, granted in advance.
///
/// Distinct from a movement record: a gate pass is approved before the fact, a movement record
/// is what actually happened. A pass may be approved and never used.
/// </summary>
public sealed class GatePassEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }

    /// <summary>DayPass or MultiDayPass.</summary>
    public string PassType { get; set; } = "DayPass";

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    /// <summary>Set on a day pass to bound the hours; null means the whole day.</summary>
    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }

    public string Purpose { get; set; } = string.Empty;
    public string? DestinationLabel { get; set; }

    /// <summary>Pending, Approved, Rejected, Withdrawn or Expired.</summary>
    public string State { get; set; } = "Pending";

    /// <summary>
    /// What a gate scanner reads. Issued only on approval, random, and unrelated to the employee
    /// number on the identity card — a number anyone can photograph identifies a person, it does
    /// not prove they hold a pass.
    /// </summary>
    public string? VerificationToken { get; set; }

    public string? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }
    public string? DecisionComment { get; set; }

    public DateTimeOffset RequestedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>
/// A scan of a pass at a gate. Append-only: the record of who was let through, and when, is
/// evidence, so a second scan adds a row rather than overwriting the first.
/// </summary>
public sealed class GatePassScanEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long GatePassId { get; set; }

    /// <summary>Exit or Entry.</summary>
    public string Direction { get; set; } = "Exit";

    public DateTimeOffset ScannedUtc { get; set; }
    public string? ScannedBy { get; set; }
    public string? GateLabel { get; set; }
}

/// <summary>
/// Business travel that still counts as working time. See <c>OnDutyRequestRecord</c> for why this
/// is not a gate pass and not leave.
/// </summary>
public sealed class OnDutyRequestEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }

    public string Purpose { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;

    /// <summary>Pending, Approved, Rejected or Withdrawn.</summary>
    public string State { get; set; } = "Pending";

    public string? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }
    public string? DecisionComment { get; set; }

    public DateTimeOffset RequestedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
