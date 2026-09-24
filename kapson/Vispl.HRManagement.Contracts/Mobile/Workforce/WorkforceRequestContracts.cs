using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Workforce;

// Attendance queries ---------------------------------------------------------

public sealed class AttendanceRangeQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [StringLength(64)] public string? TimeZoneId { get; set; }
}

public sealed class AttendanceRegisterQuery : MobileListQuery
{
    [StringLength(80)] public string? Department { get; set; }
    [StringLength(80)] public string? Location { get; set; }
    [StringLength(40)] public string? Status { get; set; }
    public bool ExceptionsOnly { get; set; }
}

public sealed class TeamAttendanceQuery : MobileListQuery
{
    public DateOnly? Date { get; set; }
    [StringLength(40)] public string? Status { get; set; }
}

// Attendance corrections (WF-003) --------------------------------------------

public sealed class CreateAttendanceCorrectionRequest
{
    [Required] public DateOnly Date { get; set; }
    [Required, StringLength(40)] public string ReasonCode { get; set; } = string.Empty;
    [StringLength(2000)] public string? Comment { get; set; }
    public IReadOnlyList<RequestedPunchDto> RequestedPunches { get; set; } = [];
    public IReadOnlyList<string> EvidenceUploadIds { get; set; } = [];
}

public sealed record RequestedPunchDto(string Type, DateTimeOffset OccurredUtc);

public sealed class AttendanceCorrectionDecisionRequest
{
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class AttendanceCorrectionListQuery : MobileListQuery
{
    [StringLength(40)] public string? State { get; set; }
    public bool AwaitingMyDecisionOnly { get; set; }
}

// Attendance exceptions and overtime (WF-004) --------------------------------

public sealed record AttendanceExceptionDto(
    string Id,
    string EmployeeId,
    DateOnly Date,
    string ExceptionType,
    TimeSpan CalculatedDuration,
    StatusDto Status,
    IReadOnlyList<AttendancePunchDto> Evidence,
    string? PolicyCode,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);

public sealed class AttendanceExceptionListQuery : MobileListQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [StringLength(40)] public string? ExceptionType { get; set; }
    [StringLength(40)] public string? State { get; set; }
}

public sealed class ConfirmAttendanceExceptionRequest
{
    public bool Confirmed { get; set; }
    [StringLength(2000)] public string? Justification { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class ResolveAttendanceExceptionRequest
{
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    public TimeSpan? ApprovedDuration { get; set; }
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

// Shifts (WF-005) -------------------------------------------------------------

public sealed class ShiftRosterQuery : MobileListQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [StringLength(80)] public string? Department { get; set; }
    [StringLength(80)] public string? Location { get; set; }
}

public sealed class UpdateShiftAssignmentRequest
{
    [StringLength(40)] public string? ShiftCode { get; set; }
    public DateOnly? Date { get; set; }
    [StringLength(80)] public string? LocationCode { get; set; }
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class RequestShiftSwapRequest
{
    [Required, StringLength(64)] public string CounterpartyEmployeeId { get; set; } = string.Empty;
    [Required, StringLength(64)] public string CounterpartyAssignmentId { get; set; } = string.Empty;
    [StringLength(1000)] public string? Reason { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed record ShiftSwapResultDto(
    string SwapId,
    string State,
    ShiftAssignmentDto Requester,
    ShiftAssignmentDto Counterparty,
    IReadOnlyList<string> Warnings,
    string? RowVersion);

public sealed class RespondToShiftSwapRequest
{
    public bool Accept { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class AcknowledgeShiftRequest
{
    [StringLength(64)] public string? RowVersion { get; set; }
}

// Leave (WF-001, WF-002) -------------------------------------------------------

public sealed class LeaveBalanceQuery
{
    public int? Year { get; set; }
    [StringLength(64)] public string? EmployeeId { get; set; }
}

public sealed class LeaveRequestListQuery : MobileListQuery
{
    [StringLength(40)] public string? State { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public sealed class TeamLeaveQuery : MobileListQuery
{
    [StringLength(40)] public string? State { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public bool AwaitingMyDecisionOnly { get; set; }
}

public sealed class LeaveDecisionRequest
{
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(2000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed class WithdrawLeaveRequest
{
    [StringLength(40)] public string? ReasonCode { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
    [StringLength(64)] public string? RowVersion { get; set; }
}

public sealed record LeaveCalendarEntryDto(
    DateOnly Date,
    PersonReferenceDto Employee,
    string LeaveTypeCode,
    string LeaveTypeName,
    decimal Days,
    StatusDto Status);

public sealed record LeaveCalendarDto(
    DateRangeDto Range,
    IReadOnlyList<LeaveCalendarEntryDto> Entries,
    IReadOnlyList<DateOnly> Holidays);

public sealed record LeaveAdjustmentCreditDto(
    string Id,
    string SourceCode,
    DateOnly EarnedOn,
    DateOnly ExpiresOn,
    decimal Days,
    bool IsEncashable,
    MoneyDto? EncashmentValue);

public sealed record LeaveAdjustmentDto(
    string Id,
    string Kind,
    IReadOnlyList<string> CreditIds,
    decimal Days,
    MoneyDto? Value,
    StatusDto Status,
    IReadOnlyList<WorkflowStepDto> Workflow,
    string? RowVersion);

public sealed class LeaveAdjustmentRequest
{
    [Required, StringLength(20)] public string Kind { get; set; } = string.Empty;
    public IReadOnlyList<string> CreditIds { get; set; } = [];
    [StringLength(1000)] public string? Comment { get; set; }
}
