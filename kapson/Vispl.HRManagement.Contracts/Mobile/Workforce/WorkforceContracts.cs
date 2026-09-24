using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Workforce;

public sealed record AttendanceDashboardDto(
    DateOnly Date,
    IReadOnlyDictionary<string, int> Counts,
    IReadOnlyList<decimal> Trend,
    DateTimeOffset GeneratedUtc);

public sealed record AttendancePunchDto(
    string Id,
    string Type,
    DateTimeOffset OccurredUtc,
    string Source,
    string? LocationLabel);

public sealed record AttendanceDayDto(
    DateOnly Date,
    string? ShiftCode,
    StatusDto Status,
    IReadOnlyList<AttendancePunchDto> Punches,
    TimeSpan Worked,
    TimeSpan Expected,
    IReadOnlyList<string> Exceptions,
    IReadOnlyList<ActionAvailabilityDto> Actions,

    /// <summary>
    /// The chip under a Recent Attendance row (6.png): on-time, late, half-day, leave, absent,
    /// holiday or weekly-off. Late is decided by the employee's shift start and grace — the same
    /// resolver the HR portal's late-arrival report uses.
    /// </summary>
    StatusDto? Badge = null,

    /// <summary>First in-punch, tenant local time.</summary>
    TimeOnly? CheckIn = null,

    /// <summary>Last out-punch, tenant local time.</summary>
    TimeOnly? CheckOut = null);

public sealed record PunchStateDto(
    string State,
    AttendancePunchDto? LastPunch,
    string? NextAction,
    DateTimeOffset ServerUtc,
    string? PolicySummary);

public sealed class PunchRequest
{
    [Required, RegularExpression("^(in|out)$")] public string Action { get; set; } = string.Empty;
    [Required, StringLength(200)] public string DeviceId { get; set; } = string.Empty;
    [Required, StringLength(200)] public string ClientEventId { get; set; } = string.Empty;
    public DateTimeOffset ClientOccurredUtc { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    [StringLength(4000)] public string? Attestation { get; set; }
}

public sealed record PunchResultDto(
    string PunchId,
    string State,
    DateTimeOffset AcceptedUtc,
    string? Warning,
    string? NextAction);

public sealed record AttendanceCorrectionDto(
    string Id,
    DateOnly Date,
    IReadOnlyList<AttendancePunchDto> OriginalPunches,
    IReadOnlyList<AttendancePunchDto> RequestedPunches,
    string Reason,
    StatusDto Status,
    IReadOnlyList<WorkflowStepDto> Workflow,
    string? RowVersion,

    /// <summary>Whose correction it is — filled for approvers, who see everyone's.</summary>
    string? EmployeeName = null);

public sealed record ShiftAssignmentDto(
    string Id,
    string ShiftCode,
    DateTimeOffset StartsUtc,
    DateTimeOffset EndsUtc,
    string TimeZoneId,
    string LocationCode,
    PersonReferenceDto Employee,
    StatusDto Status,
    string? RowVersion);

public sealed record ShiftRosterDto(
    DateRangeDto Range,
    IReadOnlyList<ShiftAssignmentDto> Assignments,
    int UnassignedCount);

public sealed record LeaveBalanceDto(
    string PolicyId,
    string LeaveTypeCode,
    string LeaveTypeName,
    int Year,
    decimal Entitled,
    decimal Accrued,
    decimal Used,
    decimal Pending,
    decimal Available,
    string Unit,
    string ToneKey = LeaveTones.Blue,
    string Emoji = LeaveTones.DefaultEmoji);

/// <summary>
/// The colour family a leave type is drawn in — the tinted header strip, avatar and employee
/// number on an approval card, and the history row icon. HR picks one per policy; the app maps
/// the key to its own palette, so the API never sends a hex value.
/// </summary>
public static class LeaveTones
{
    public const string Blue = "blue";
    public const string Pink = "pink";
    public const string Green = "green";
    public const string Purple = "purple";
    public const string Teal = "teal";
    public const string Amber = "amber";

    public const string DefaultEmoji = "🗓️";

    public static IReadOnlyList<string> All { get; } = [Blue, Pink, Green, Purple, Teal, Amber];

    public static string Normalize(string? tone) =>
        tone is not null && All.Contains(tone.Trim().ToLowerInvariant()) ? tone.Trim().ToLowerInvariant() : Blue;
}

public sealed record LeaveRequestListItemDto(
    string Id,
    string LeaveTypeCode,
    DateOnly From,
    DateOnly To,
    decimal Days,
    StatusDto Status,
    DateTimeOffset SubmittedUtc,
    string? LeaveTypeName = null,
    string ToneKey = LeaveTones.Blue,
    string Emoji = LeaveTones.DefaultEmoji);

public sealed record LeaveRequestDetailDto(
    LeaveRequestListItemDto Summary,
    string Reason,
    IReadOnlyList<AttachmentMetadataDto> Attachments,
    IReadOnlyList<WorkflowStepDto> Workflow,
    IReadOnlyList<ActionAvailabilityDto> Actions,
    string? RowVersion);

public sealed class LeaveCalculationRequest
{
    [Required, StringLength(80)] public string LeaveTypeCode { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public bool IsFirstDayPartial { get; set; }
    public bool IsLastDayPartial { get; set; }
}

public sealed record LeaveCalculationDto(
    decimal RequestedDays,
    decimal ChargeableDays,
    decimal BalanceBefore,
    decimal BalanceAfter,
    IReadOnlyList<DateOnly> ExcludedDates,
    IReadOnlyList<string> Warnings,
    string CalculationToken);

public sealed class CreateLeaveRequest
{
    [Required, StringLength(80)] public string LeaveTypeCode { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public bool IsFirstDayPartial { get; set; }
    public bool IsLastDayPartial { get; set; }
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
    [Required, StringLength(1000)] public string CalculationToken { get; set; } = string.Empty;
    public IReadOnlyList<string> AttachmentIds { get; set; } = [];
}

public sealed record LeavePolicySummaryDto(
    string Id,
    string Code,
    string Name,
    string Unit,
    string Version,
    string ToneKey = LeaveTones.Blue,
    string Emoji = LeaveTones.DefaultEmoji,
    bool AllowsHalfDay = true);

public sealed record LeavePolicyDetailDto(
    LeavePolicySummaryDto Summary,
    string Eligibility,
    string AccrualRule,
    string CarryForwardRule,
    string NoticeRule,
    IReadOnlyList<string> Documents);
