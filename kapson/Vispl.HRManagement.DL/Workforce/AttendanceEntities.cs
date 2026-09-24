namespace Vispl.HRManagement.DL.Workforce;

/// <summary>
/// One raw clock event. Punches are append-only: a mistake is corrected by an approved
/// correction that supersedes them, never by editing history, so the original record of what
/// the device reported survives any dispute.
/// </summary>
public sealed class AttendancePunchEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }

    /// <summary>The working day this belongs to, in the employee's zone.</summary>
    public DateOnly WorkDate { get; set; }

    /// <summary>In or Out.</summary>
    public string Type { get; set; } = "In";

    public DateTimeOffset OccurredUtc { get; set; }

    /// <summary>Device, Web, Correction or Admin — how the punch got here.</summary>
    public string Source { get; set; } = "Device";

    public string? DeviceId { get; set; }
    public string? LocationLabel { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    /// <summary>
    /// Client-supplied identifier for one physical tap. Unique per employee, so a retry over
    /// a flaky connection cannot record the same punch twice.
    /// </summary>
    public string? ClientEventId { get; set; }

    /// <summary>Set when an approved correction replaced this punch.</summary>
    public long? SupersededByCorrectionId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// The computed state of one employee-day: worked time, expected time and status. Derived from
/// punches, stored so the register can be queried without recomputing every row.
/// </summary>
public sealed class AttendanceDayEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }

    public string? ShiftCode { get; set; }

    /// <summary>Present, Absent, Leave, Holiday, WeeklyOff or Partial.</summary>
    public string Status { get; set; } = "Absent";

    public int WorkedMinutes { get; set; }
    public int ExpectedMinutes { get; set; }

    /// <summary>Exception codes raised for the day, as a JSON array.</summary>
    public string ExceptionsJson { get; set; } = "[]";

    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class AttendanceCorrectionEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }

    public string ReasonCode { get; set; } = string.Empty;
    public string? Comment { get; set; }

    /// <summary>The punches as they were when the request was raised, for the before/after view.</summary>
    public string OriginalPunchesJson { get; set; } = "[]";

    /// <summary>The punches being asked for.</summary>
    public string RequestedPunchesJson { get; set; } = "[]";

    /// <summary>Pending, Approved, Rejected or Withdrawn.</summary>
    public string State { get; set; } = "Pending";

    public string? DecisionReasonCode { get; set; }
    public string? DecisionComment { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }

    public DateTimeOffset SubmittedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class AttendanceExceptionEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public DateOnly WorkDate { get; set; }

    /// <summary>MissingPunch, LateIn, EarlyOut, ShortHours or Overtime.</summary>
    public string ExceptionType { get; set; } = string.Empty;

    /// <summary>How much: minutes late, minutes short, minutes of overtime.</summary>
    public int DurationMinutes { get; set; }

    /// <summary>Open, Confirmed, Approved or Dismissed.</summary>
    public string State { get; set; } = "Open";

    public string? PolicyCode { get; set; }
    public string? EmployeeJustification { get; set; }
    public int? ApprovedMinutes { get; set; }
    public string? DecisionReasonCode { get; set; }
    public string? DecisionComment { get; set; }
    public string? DecidedBy { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }

    public DateTimeOffset DetectedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
