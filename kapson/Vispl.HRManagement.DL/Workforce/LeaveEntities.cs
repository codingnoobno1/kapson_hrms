namespace Vispl.HRManagement.DL.Workforce;

/// <summary>
/// A leave type and the rules that govern it. Policies carry a version rather than being
/// edited anonymously, so a request approved last year can still be explained by the rules
/// that applied when it was approved.
/// </summary>
public sealed class LeavePolicyEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;

    /// <summary>Stable code such as AL, SL or CL.</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Colour family on the employee app (blue, pink, green, purple, teal, amber).</summary>
    public string ToneKey { get; set; } = "blue";

    /// <summary>Icon shown beside the type in leave history and approval cards.</summary>
    public string Emoji { get; set; } = "🗓️";

    /// <summary>Days or Hours.</summary>
    public string Unit { get; set; } = "Days";

    public string Version { get; set; } = "1";

    /// <summary>Full-year entitlement before accrual, carry-forward and adjustment.</summary>
    public decimal AnnualEntitlement { get; set; }

    /// <summary>Annual, Monthly or None — how the entitlement becomes available.</summary>
    public string AccrualMode { get; set; } = "Annual";

    /// <summary>Maximum days that may cross into the next year.</summary>
    public decimal CarryForwardCap { get; set; }

    /// <summary>Days of notice normally expected before the first day of leave.</summary>
    public int NoticeDays { get; set; }

    /// <summary>A request longer than this needs supporting evidence; null disables the rule.</summary>
    public decimal? DocumentRequiredAboveDays { get; set; }

    public bool AllowsHalfDay { get; set; } = true;

    /// <summary>Whether a request may take the balance below zero.</summary>
    public bool AllowsNegativeBalance { get; set; }

    /// <summary>Whether weekends and holidays inside the range are charged.</summary>
    public bool CountsNonWorkingDays { get; set; }

    public string EligibilityText { get; set; } = string.Empty;
    public string AccrualRuleText { get; set; } = string.Empty;
    public string CarryForwardRuleText { get; set; } = string.Empty;
    public string NoticeRuleText { get; set; } = string.Empty;

    /// <summary>JSON array of document names published with the policy.</summary>
    public string DocumentsJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>
/// The granted side of an employee balance for one policy year: entitlement, accrual,
/// carry-forward and manual adjustment.
///
/// Consumption is deliberately absent. Used and pending days are derived from the request rows
/// every time a balance is read, so a stored counter can never drift away from the requests it
/// is supposed to summarise.
/// </summary>
public sealed class LeaveBalanceEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public long PolicyId { get; set; }
    public int Year { get; set; }

    public decimal Entitled { get; set; }
    public decimal Accrued { get; set; }
    public decimal CarriedForward { get; set; }
    public decimal AdjustmentDays { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>One application for leave, and the decision made on it.</summary>
public sealed class LeaveRequestEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public long PolicyId { get; set; }
    public string LeaveTypeCode { get; set; } = string.Empty;

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public bool IsFirstDayPartial { get; set; }
    public bool IsLastDayPartial { get; set; }

    /// <summary>Chargeable days, as computed by the server at submission.</summary>
    public decimal Days { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Pending, Approved, Rejected, Withdrawn or Cancelled.</summary>
    public string State { get; set; } = "Pending";

    /// <summary>JSON array of upload ids supporting the request.</summary>
    public string AttachmentsJson { get; set; } = "[]";

    public DateTimeOffset SubmittedUtc { get; set; }

    public string? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }
    public string? DecisionReasonCode { get; set; }
    public string? DecisionComment { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }
}

/// <summary>
/// One charged calendar date within a request. Storing the days explicitly is what makes
/// overlap detection and the team calendar exact: both read dates rather than re-deriving them
/// from a range and a set of rules that may since have changed.
/// </summary>
public sealed class LeaveRequestDayEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long RequestId { get; set; }
    public long EmployeeId { get; set; }
    public DateOnly Date { get; set; }

    /// <summary>1 for a full day, 0.5 for a half day.</summary>
    public decimal Portion { get; set; } = 1m;
}

/// <summary>A comment or decision on a request, in the order it happened.</summary>
public sealed class LeaveRequestCommentEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long RequestId { get; set; }

    /// <summary>Submitted, Commented, Approved, Rejected or Withdrawn.</summary>
    public string Kind { get; set; } = "Commented";

    public string? AuthorUserId { get; set; }
    public string AuthorName { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>A non-working day. Holidays are excluded from chargeable leave.</summary>
public sealed class LeaveHolidayEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Null applies the holiday everywhere; a code limits it to one location.</summary>
    public string? LocationCode { get; set; }
}

/// <summary>
/// A dated block of leave that may be carried forward or encashed. Credits expire, so they are
/// tracked individually rather than as one pooled number.
/// </summary>
public sealed class LeaveCreditEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }
    public long PolicyId { get; set; }

    /// <summary>Accrual, CarryForward, CompOff or Award.</summary>
    public string SourceCode { get; set; } = "Accrual";

    public DateOnly EarnedOn { get; set; }
    public DateOnly ExpiresOn { get; set; }
    public decimal Days { get; set; }
    public bool IsEncashable { get; set; }
    public decimal? EncashmentAmount { get; set; }
    public string? EncashmentCurrency { get; set; }

    /// <summary>Set once the credit has been consumed by an adjustment.</summary>
    public long? ConsumedByAdjustmentId { get; set; }
}

/// <summary>An encashment or carry-forward request raised against specific credits.</summary>
public sealed class LeaveAdjustmentEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long EmployeeId { get; set; }

    /// <summary>Encash or CarryForward.</summary>
    public string Kind { get; set; } = "Encash";

    /// <summary>JSON array of the credit ids this adjustment consumes.</summary>
    public string CreditIdsJson { get; set; } = "[]";

    public decimal Days { get; set; }
    public decimal? Amount { get; set; }
    public string? Currency { get; set; }

    public string State { get; set; } = "Pending";
    public string? Comment { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset? DecidedUtc { get; set; }
}
