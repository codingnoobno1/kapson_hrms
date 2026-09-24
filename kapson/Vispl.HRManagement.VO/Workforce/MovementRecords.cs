namespace Vispl.HRManagement.VO.Workforce;

public sealed record MovementRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    string Kind,
    DateOnly WorkDate,
    DateTimeOffset GoingOutUtc,
    DateTimeOffset ExpectedReturnUtc,
    DateTimeOffset? ActualReturnUtc,
    string Detail,
    string? DestinationLabel,
    string State,
    string? RecordedBy,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc)
{
    /// <summary>Still out with the expected return already passed.</summary>
    public bool IsOverdue(DateTimeOffset now) =>
        State == "Open" && ActualReturnUtc is null && ExpectedReturnUtc < now;

    /// <summary>Minutes away, so far or in total.</summary>
    public int MinutesOut(DateTimeOffset now) =>
        (int)Math.Max(((ActualReturnUtc ?? now) - GoingOutUtc).TotalMinutes, 0);
}

public sealed record GatePassRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    string PassType,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime,
    TimeOnly? ToTime,
    string Purpose,
    string? DestinationLabel,
    string State,
    string? VerificationToken,
    string? DecidedByUserId,
    string? DecidedByName,
    DateTimeOffset? DecidedUtc,
    string? DecisionComment,
    DateTimeOffset RequestedUtc,
    DateTimeOffset UpdatedUtc)
{
    /// <summary>Approved and within its dates — the only state a gate should accept.</summary>
    public bool IsUsableOn(DateOnly date) =>
        State == "Approved" && FromDate <= date && ToDate >= date;
}

public sealed record GatePassScanRecord(
    long Id,
    string TenantKey,
    long GatePassId,
    string Direction,
    DateTimeOffset ScannedUtc,
    string? ScannedBy,
    string? GateLabel);

public sealed record MovementFilter(
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<long>? EmployeeIds,
    string? Kind,
    string? State,
    bool OverdueOnly);

public sealed record GatePassFilter(
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<long>? EmployeeIds,
    string? State,
    string? PassType);

/// <summary>
/// Business travel that still counts as working time — a client meeting, a conference, a site
/// visit. Distinct from a gate pass (permission to leave the premises, checked at a physical
/// gate) and from leave (time not worked): this carries no verification token, because nobody
/// scans it. It exists so attendance and payroll see the days as work, not an absence.
/// </summary>
public sealed record OnDutyRequestRecord(
    long Id,
    string TenantKey,
    long EmployeeId,
    string Purpose,
    DateOnly FromDate,
    DateOnly ToDate,
    string Location,
    string Reason,
    string State,
    string? DecidedByUserId,
    string? DecidedByName,
    DateTimeOffset? DecidedUtc,
    string? DecisionComment,
    DateTimeOffset RequestedUtc,
    DateTimeOffset UpdatedUtc);

public sealed record OnDutyFilter(
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<long>? EmployeeIds,
    string? State);
