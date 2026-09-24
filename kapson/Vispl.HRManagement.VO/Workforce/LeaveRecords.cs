namespace Vispl.HRManagement.VO.Workforce;

public sealed record LeavePolicyRecord(
    long Id,
    string TenantKey,
    string Code,
    string Name,
    string Unit,
    string Version,
    decimal AnnualEntitlement,
    string AccrualMode,
    decimal CarryForwardCap,
    int NoticeDays,
    decimal? DocumentRequiredAboveDays,
    bool AllowsHalfDay,
    bool AllowsNegativeBalance,
    bool CountsNonWorkingDays,
    string EligibilityText,
    string AccrualRuleText,
    string CarryForwardRuleText,
    string NoticeRuleText,
    string DocumentsJson,
    bool IsActive,
    string ToneKey = "blue",
    string Emoji = "🗓️");

public sealed record LeaveBalanceRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    long PolicyId,
    int Year,
    decimal Entitled,
    decimal Accrued,
    decimal CarriedForward,
    decimal AdjustmentDays);

public sealed record LeaveRequestRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    long PolicyId,
    string LeaveTypeCode,
    DateOnly FromDate,
    DateOnly ToDate,
    bool IsFirstDayPartial,
    bool IsLastDayPartial,
    decimal Days,
    string Reason,
    string State,
    string AttachmentsJson,
    DateTimeOffset SubmittedUtc,
    string? DecidedByUserId,
    string? DecidedByName,
    DateTimeOffset? DecidedUtc,
    string? DecisionReasonCode,
    string? DecisionComment,
    DateTimeOffset UpdatedUtc);

public sealed record LeaveRequestDayRecord(
    long Id,
    string TenantKey,
    long RequestId,
    long EmployeeId,
    DateOnly Date,
    decimal Portion);

public sealed record LeaveRequestCommentRecord(
    long Id,
    string TenantKey,
    long RequestId,
    string Kind,
    string? AuthorUserId,
    string AuthorName,
    string? Comment,
    DateTimeOffset CreatedUtc);

public sealed record LeaveHolidayRecord(
    long Id,
    string TenantKey,
    DateOnly Date,
    string Name,
    string? LocationCode);

public sealed record LeaveCreditRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    long PolicyId,
    string SourceCode,
    DateOnly EarnedOn,
    DateOnly ExpiresOn,
    decimal Days,
    bool IsEncashable,
    decimal? EncashmentAmount,
    string? EncashmentCurrency,
    long? ConsumedByAdjustmentId);

public sealed record LeaveAdjustmentRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    string Kind,
    string CreditIdsJson,
    decimal Days,
    decimal? Amount,
    string? Currency,
    string State,
    string? Comment,
    DateTimeOffset CreatedUtc,
    DateTimeOffset? DecidedUtc);

/// <summary>Filter for the request list, the team queue and the calendar.</summary>
public sealed record LeaveRequestFilter(
    long? EmployeeId,
    IReadOnlyList<long>? EmployeeIds,
    string? State,
    DateOnly? From,
    DateOnly? To);

/// <summary>Days consumed and awaiting decision, derived from the request rows.</summary>
public sealed record LeaveConsumptionRecord(
    long PolicyId,
    decimal UsedDays,
    decimal PendingDays);
