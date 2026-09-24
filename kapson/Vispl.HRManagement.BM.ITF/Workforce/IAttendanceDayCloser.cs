namespace Vispl.HRManagement.BM.ITF.Workforce;

/// <summary>What closing one date did, for the log and for the screen that asks for it.</summary>
public sealed record AttendanceCloseResultDto(
    string TenantKey,
    DateOnly Date,

    /// <summary>Employed on that date. The denominator everything else here divides.</summary>
    int Population,

    /// <summary>Days that already had a row, from a punch or a correction. Never touched.</summary>
    int AlreadyRecorded,

    int MarkedAbsent,
    int MarkedOnLeave,
    int MarkedRestDay,

    /// <summary>Set when the whole date was a public holiday, so nothing was expected.</summary>
    bool WasHoliday);

/// <summary>
/// Writes the attendance days nobody's device ever created.
///
/// A day is recomputed when somebody punches or a correction is approved. Somebody who simply
/// never comes in triggers neither, so their absence exists only as an inference — "rostered
/// minus present" — and never as a record. That is why the dashboard tile and the month
/// overview disagreed: the tile inferred, the table counted rows, and there were no rows.
///
/// This closes a finished date by filling only the gaps, so a punched day, a correction or an
/// earlier close is never overwritten. Running it twice changes nothing.
/// </summary>
public interface IAttendanceDayCloser
{
    /// <summary>
    /// Closes one date for one client. The date must already be over in that client's own time
    /// zone — closing a day still being worked would record absences against people who simply
    /// have not arrived yet.
    /// </summary>
    Task<AttendanceCloseResultDto> CloseAsync(
        string tenantKey, DateOnly date, CancellationToken ct = default);

    /// <summary>
    /// Closes every finished date in the window that is not closed yet, oldest first. A server
    /// that was down over a weekend catches up rather than losing those days.
    /// </summary>
    Task<IReadOnlyList<AttendanceCloseResultDto>> CatchUpAsync(
        string tenantKey, int lookbackDays, CancellationToken ct = default);
}
