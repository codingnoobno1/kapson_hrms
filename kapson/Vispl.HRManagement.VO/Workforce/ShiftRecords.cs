namespace Vispl.HRManagement.VO.Workforce;

public sealed record ShiftDefinitionRecord(
    long Id,
    string TenantKey,
    string Code,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int BreakMinutes,
    int GraceMinutes,
    string WorkingDays,
    bool CrossesMidnight,
    bool IsDefault,
    bool IsActive)
{
    /// <summary>Paid minutes: the span less the unpaid break, midnight handled.</summary>
    public int ExpectedMinutes
    {
        get
        {
            var span = CrossesMidnight
                ? (TimeSpan.FromHours(24) - StartTime.ToTimeSpan()) + EndTime.ToTimeSpan()
                : EndTime.ToTimeSpan() - StartTime.ToTimeSpan();

            return Math.Max((int)span.TotalMinutes - BreakMinutes, 0);
        }
    }

    /// <summary>Whether this shift is worked on that weekday, read off the mask.</summary>
    public bool WorksOn(DayOfWeek day) =>
        WorkingDays.Length == 7 &&
        char.ToUpperInvariant(WorkingDays[(int)day]) == 'Y';
}

public sealed record ShiftAssignmentRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    long ShiftDefinitionId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    DateTimeOffset AssignedUtc,
    string? AssignedBy,
    string? Note);

/// <summary>
/// What one employee was expected to do on one date.
///
/// <paramref name="IsFallback"/> is the honest part: it says the answer came from the tenant
/// default because nobody assigned this person a shift. Callers surface that rather than
/// presenting an assumption as a roster.
/// </summary>
public sealed record ExpectedShiftRecord(
    long EmployeeId,
    DateOnly Date,
    string ShiftCode,
    string ShiftName,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int ExpectedMinutes,
    int GraceMinutes,
    bool IsWorkingDay,
    bool IsFallback);
