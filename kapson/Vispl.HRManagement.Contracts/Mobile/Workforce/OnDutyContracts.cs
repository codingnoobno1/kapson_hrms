using System.ComponentModel.DataAnnotations;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Contracts.Mobile.Workforce;

// ---- On duty ----------------------------------------------------------------
//
// Business travel that still counts as working time: a client meeting, a conference, a site
// visit spanning one or more days. Distinct from a gate pass (permission to be off the premises,
// checked at a physical gate) and from leave (time not worked) — on duty is neither. It carries
// no verification token because nobody scans it; it exists so attendance and payroll can see the
// days were work, not an unexplained absence.

public sealed record OnDutyRowDto(
    string Id,
    string EmployeeId,
    string DisplayName,
    string? DepartmentCode,
    string? Designation,
    string Purpose,
    DateOnly FromDate,
    DateOnly ToDate,
    string Location,
    string Reason,
    StatusDto Status,
    string? DecidedByName,
    DateTimeOffset? DecidedUtc,
    string? DecisionComment,
    IReadOnlyList<ActionAvailabilityDto> Actions);

public sealed class RequestOnDutyRequest
{
    [Required, StringLength(200)] public string Purpose { get; set; } = string.Empty;
    [Required] public DateOnly FromDate { get; set; }
    [Required] public DateOnly ToDate { get; set; }
    [Required, StringLength(200)] public string Location { get; set; } = string.Empty;
    [Required, StringLength(2000)] public string Reason { get; set; } = string.Empty;
}

public sealed class OnDutyDecisionRequest
{
    [Required, StringLength(20)] public string Decision { get; set; } = string.Empty;
    [StringLength(2000)] public string? Comment { get; set; }
}
