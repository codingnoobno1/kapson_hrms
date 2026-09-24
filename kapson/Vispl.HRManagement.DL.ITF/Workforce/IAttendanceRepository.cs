using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.DL.ITF.Workforce;

public interface IAttendanceRepository
{
    // Punches
    Task<IReadOnlyList<AttendancePunchRecord>> GetPunchesAsync(
        string tenantKey, long employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    /// <summary>Every live punch for a population across a range — the monthly report's in / out times.</summary>
    Task<IReadOnlyList<AttendancePunchRecord>> GetPunchesForAsync(
        string tenantKey, IReadOnlyList<long> employeeIds, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);

    Task<AttendancePunchRecord?> GetLastPunchAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default);

    /// <summary>Returns the existing punch when this client event was already recorded.</summary>
    Task<AttendancePunchRecord?> FindByClientEventAsync(
        string tenantKey, long employeeId, string clientEventId, CancellationToken cancellationToken = default);

    Task<long> AddPunchAsync(AttendancePunchRecord punch, CancellationToken cancellationToken = default);

    Task SupersedePunchesAsync(
        string tenantKey, long employeeId, DateOnly workDate, long correctionId, CancellationToken cancellationToken = default);

    // Days
    Task<IReadOnlyList<AttendanceDayRecord>> QueryDaysAsync(
        string tenantKey, AttendanceDayFilter filter, CancellationToken cancellationToken = default);

    Task<AttendanceDayRecord?> GetDayAsync(
        string tenantKey, long employeeId, DateOnly workDate, CancellationToken cancellationToken = default);

    Task<long> UpsertDayAsync(AttendanceDayRecord day, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
        string tenantKey, DateOnly workDate, CancellationToken cancellationToken = default);

    /// <summary>Status counts for one day, limited to a population — the dashboard's scope.</summary>
    Task<IReadOnlyDictionary<string, int>> CountByStatusForAsync(
        string tenantKey, DateOnly workDate, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// First in-punch per employee for a day, so lateness can be measured against a shift
    /// without loading every punch of every employee.
    /// </summary>
    Task<IReadOnlyDictionary<long, DateTimeOffset>> GetFirstInPunchAsync(
        string tenantKey, DateOnly workDate, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// First in-punch per employee per day across a range. Feeds the cumulative lateness
    /// columns, which would otherwise need one query per employee per day.
    /// </summary>
    Task<IReadOnlyList<(long EmployeeId, DateOnly WorkDate, DateTimeOffset FirstIn)>>
        GetFirstInPunchRangeAsync(
            string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
            CancellationToken cancellationToken = default);

    /// <summary>
    /// In and out counts per employee-day. A missing punch is not recorded as "which side" on
    /// the exception, so the side is derived from parity: more ins than outs means the day was
    /// never closed.
    /// </summary>
    Task<IReadOnlyList<(long EmployeeId, DateOnly WorkDate, int InCount, int OutCount)>>
        GetPunchCountsAsync(
            string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
            CancellationToken cancellationToken = default);

    /// <summary>Exceptions across a population — the miss-punch and exception queues.</summary>
    Task<IReadOnlyList<AttendanceExceptionRecord>> QueryExceptionsForAsync(
        string tenantKey, IReadOnlyList<long> employeeIds, DateOnly from, DateOnly to,
        string? exceptionType, string? state, CancellationToken cancellationToken = default);

    // Corrections
    Task<IReadOnlyList<AttendanceCorrectionRecord>> QueryCorrectionsAsync(
        string tenantKey, long? employeeId, string? state, CancellationToken cancellationToken = default);

    Task<AttendanceCorrectionRecord?> GetCorrectionAsync(
        string tenantKey, long correctionId, CancellationToken cancellationToken = default);

    Task<AttendanceCorrectionRecord?> GetPendingCorrectionAsync(
        string tenantKey, long employeeId, DateOnly workDate, CancellationToken cancellationToken = default);

    Task<long> UpsertCorrectionAsync(
        AttendanceCorrectionRecord correction, CancellationToken cancellationToken = default);

    // Exceptions
    Task<IReadOnlyList<AttendanceExceptionRecord>> QueryExceptionsAsync(
        string tenantKey, long? employeeId, DateOnly? from, DateOnly? to, string? exceptionType, string? state,
        CancellationToken cancellationToken = default);

    Task<AttendanceExceptionRecord?> GetExceptionAsync(
        string tenantKey, long exceptionId, CancellationToken cancellationToken = default);

    Task UpsertExceptionAsync(AttendanceExceptionRecord exception, CancellationToken cancellationToken = default);

    Task RemoveExceptionsAsync(
        string tenantKey, long employeeId, DateOnly workDate, IReadOnlyList<string> keepTypes,
        CancellationToken cancellationToken = default);
}
