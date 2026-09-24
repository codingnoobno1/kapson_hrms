namespace Vispl.HRManagement.VO.Workforce;

public sealed record AttendancePunchRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    DateOnly WorkDate,
    string Type,
    DateTimeOffset OccurredUtc,
    string Source,
    string? DeviceId,
    string? LocationLabel,
    decimal? Latitude,
    decimal? Longitude,
    string? ClientEventId,
    long? SupersededByCorrectionId,
    DateTimeOffset CreatedUtc);

public sealed record AttendanceDayRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    DateOnly WorkDate,
    string? ShiftCode,
    string Status,
    int WorkedMinutes,
    int ExpectedMinutes,
    string ExceptionsJson,
    DateTimeOffset UpdatedUtc);

public sealed record AttendanceCorrectionRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    DateOnly WorkDate,
    string ReasonCode,
    string? Comment,
    string OriginalPunchesJson,
    string RequestedPunchesJson,
    string State,
    string? DecisionReasonCode,
    string? DecisionComment,
    string? DecidedBy,
    DateTimeOffset? DecidedUtc,
    DateTimeOffset SubmittedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record AttendanceExceptionRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    DateOnly WorkDate,
    string ExceptionType,
    int DurationMinutes,
    string State,
    string? PolicyCode,
    string? EmployeeJustification,
    int? ApprovedMinutes,
    string? DecisionReasonCode,
    string? DecisionComment,
    string? DecidedBy,
    DateTimeOffset? DecidedUtc,
    DateTimeOffset DetectedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record AttendanceDayFilter(
    DateOnly? From,
    DateOnly? To,
    long? EmployeeId,
    IReadOnlyList<long>? EmployeeIds,
    string? Status,
    bool ExceptionsOnly);
