using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Mobile.Workforce;

/// <summary>
/// HR changing one employee-day from the Employee Monthly Attendance Report (phase D, D-1). Applied at
/// once as an approved correction: the original punches are kept (superseded, not deleted), the reason
/// is recorded and the employee is told. Times are the tenant's local wall clock.
/// </summary>
public sealed class HrAttendanceDayEditRequest
{
    [Required] public string EmployeeId { get; set; } = string.Empty;

    [Required] public DateOnly Date { get; set; }

    public TimeOnly? CheckIn { get; set; }

    public TimeOnly? CheckOut { get; set; }

    /// <summary>Clears the day's punches, so it recomputes as absent (or weekly off on a rest day).</summary>
    public bool MarkAbsent { get; set; }

    [Required, StringLength(500)] public string Reason { get; set; } = string.Empty;
}
