using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Workforce;

// ---- Movement -------------------------------------------------------------

public sealed record MovementRowDto(
    string Id,
    string EmployeeId,
    string DisplayName,
    string? DepartmentCode,
    string? Designation,
    string Kind,
    DateOnly WorkDate,
    DateTimeOffset GoingOutUtc,
    DateTimeOffset ExpectedReturnUtc,
    DateTimeOffset? ActualReturnUtc,
    string Detail,
    string? DestinationLabel,
    string State,
    bool IsOverdue,
    int MinutesOut);

public sealed record MovementBoardDto(
    IReadOnlyList<MovementRowDto> Personal,
    IReadOnlyList<MovementRowDto> Official,
    int OverdueCount,
    DateTimeOffset GeneratedUtc);

public sealed class MovementQuery
{
    public DateOnly? Date { get; set; }

    /// <summary>Personal or Official. Blank returns both.</summary>
    [StringLength(20)] public string? Kind { get; set; }

    public bool OverdueOnly { get; set; }
}

public sealed class DeclareMovementRequest
{
    [Required, RegularExpression("^(Personal|Official)$", ErrorMessage = "Going out is personal or official.")]
    public string Kind { get; set; } = "Personal";

    /// <summary>Blank means now. A future time is refused.</summary>
    public DateTimeOffset? GoingOutUtc { get; set; }

    [Required] public DateTimeOffset ExpectedReturnUtc { get; set; }

    [Required, StringLength(500)] public string Detail { get; set; } = string.Empty;

    [StringLength(200)] public string? DestinationLabel { get; set; }
}

public sealed class ReturnMovementRequest
{
    /// <summary>Blank means now.</summary>
    public DateTimeOffset? ActualReturnUtc { get; set; }
}

// ---- Gate passes ----------------------------------------------------------

public sealed record GatePassRowDto(
    string Id,
    string EmployeeId,
    string DisplayName,
    string? DepartmentCode,
    string? Designation,
    string PassType,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeOnly? FromTime,
    TimeOnly? ToTime,
    string Purpose,
    string? DestinationLabel,
    StatusDto Status,
    string? DecidedByName,
    DateTimeOffset? DecidedUtc,
    string? DecisionComment,
    int ScanCount,
    IReadOnlyList<ActionAvailabilityDto> Actions);

public sealed record GatePassBoardDto(
    CursorPage<GatePassRowDto> Rows,
    int PendingCount,
    DateTimeOffset GeneratedUtc);

/// <summary>
/// Returned to the holder of an approved pass, and to nobody else.
///
/// The token is what a gate scanner reads. It is issued on approval and is unrelated to the
/// employee number printed on the identity card: a number anyone can photograph identifies a
/// person, it does not prove they hold a pass.
/// </summary>
public sealed record GatePassCredentialDto(
    string PassId,
    string VerificationToken,
    DateOnly ValidFrom,
    DateOnly ValidTo);

public sealed class GatePassQuery : MobileListQuery
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    [StringLength(20)] public string? State { get; set; }
    [StringLength(20)] public string? PassType { get; set; }
}

public sealed class RequestGatePassRequest
{
    [Required, RegularExpression("^(DayPass|MultiDayPass)$")]
    public string PassType { get; set; } = "DayPass";

    [Required] public DateOnly FromDate { get; set; }
    [Required] public DateOnly ToDate { get; set; }

    public TimeOnly? FromTime { get; set; }
    public TimeOnly? ToTime { get; set; }

    [Required, StringLength(500)] public string Purpose { get; set; } = string.Empty;
    [StringLength(200)] public string? DestinationLabel { get; set; }
}

public sealed class GatePassDecisionRequest
{
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    [StringLength(2000)] public string? Comment { get; set; }
}

public sealed class ScanGatePassRequest
{
    [Required, StringLength(64)] public string Token { get; set; } = string.Empty;

    [Required, RegularExpression("^(Exit|Entry)$")]
    public string Direction { get; set; } = "Exit";

    [StringLength(120)] public string? GateLabel { get; set; }
}

/// <summary>
/// What a gate is told. Deliberately thin: a guard needs to know whether to open the barrier and
/// who is in front of them, not the person's department, purpose or history.
/// </summary>
public sealed record GatePassScanResultDto(
    bool IsValid,
    string Outcome,
    string? EmployeeName,
    string? EmployeeNumber,
    string? PassType,
    DateOnly? ValidTo,
    string Message);
