using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.ITF.Mobile;

/// <summary>
/// Answers "what was this person expected to do on this date".
///
/// One resolver, used by attendance, the dashboard and the reports, so lateness cannot mean one
/// thing on an employee's own screen and another on the HR table. Before this existed every
/// caller assumed a fixed 09:00.
/// </summary>
public interface IShiftScheduleService
{
    /// <summary>
    /// Resolves the expected shift for a population across a date range, in one pass.
    ///
    /// Callers ask the returned schedule per employee-day rather than querying again, because a
    /// month of a company's rosters is one query, not thirty thousand.
    /// </summary>
    Task<ShiftSchedule> ResolveAsync(
        string tenantKey,
        IReadOnlyList<long> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken ct);
}

/// <summary>
/// The resolved expectations for one population and range.
///
/// <see cref="UsesFallbackOnly"/> reports that nobody in the population has a real assignment,
/// so every figure derived from it rests on the tenant default. Screens say so rather than
/// implying a roster was consulted.
/// </summary>
public sealed class ShiftSchedule
{
    private readonly IReadOnlyDictionary<(long EmployeeId, DateOnly Date), ExpectedShiftRecord> entries;

    public ShiftSchedule(
        IReadOnlyDictionary<(long, DateOnly), ExpectedShiftRecord> entries,
        bool usesFallbackOnly,
        ShiftDefinitionRecord? fallback)
    {
        this.entries = entries;
        UsesFallbackOnly = usesFallbackOnly;
        Fallback = fallback;
    }

    public bool UsesFallbackOnly { get; }

    public ShiftDefinitionRecord? Fallback { get; }

    public ExpectedShiftRecord? For(long employeeId, DateOnly date) =>
        entries.GetValueOrDefault((employeeId, date));

    /// <summary>
    /// Minutes late past the shift's own grace, floored at zero. Returns null when the day was
    /// not a working day for that person — an arrival on a rest day is not lateness.
    /// </summary>
    public int? DelayMinutes(long employeeId, DateOnly date, DateTimeOffset arrival)
    {
        var expected = For(employeeId, date);

        if (expected is null || !expected.IsWorkingDay)
        {
            return null;
        }

        var arrivedAt = TimeOnly.FromDateTime(arrival.UtcDateTime);
        var cutoff = expected.StartTime.AddMinutes(expected.GraceMinutes);

        return arrivedAt <= cutoff
            ? 0
            : (int)Math.Round((arrivedAt - expected.StartTime).TotalMinutes);
    }
}
