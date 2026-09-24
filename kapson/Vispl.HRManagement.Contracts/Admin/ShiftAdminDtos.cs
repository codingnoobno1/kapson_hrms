using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

/// <summary>
/// A shift as the admin panel edits it. These values decide when someone is late and how long
/// a day is expected to be, so validation here is stricter than storage requires.
/// </summary>
public sealed class ShiftDefinitionAdminDto
{
    public long Id { get; set; }

    [Required, RegularExpression("^[a-z0-9-]+$")]
    public string TenantKey { get; set; } = string.Empty;

    [Required, RegularExpression("^[A-Z0-9_-]+$", ErrorMessage = "Use upper-case letters, digits, underscore or hyphen.")]
    [StringLength(40)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(120)]
    public string Name { get; set; } = string.Empty;

    public TimeOnly StartTime { get; set; } = new(9, 0);
    public TimeOnly EndTime { get; set; } = new(18, 0);

    [Range(0, 480)] public int BreakMinutes { get; set; } = 60;
    [Range(0, 240)] public int GraceMinutes { get; set; } = 15;

    public bool Monday { get; set; } = true;
    public bool Tuesday { get; set; } = true;
    public bool Wednesday { get; set; } = true;
    public bool Thursday { get; set; } = true;
    public bool Friday { get; set; } = true;
    public bool Saturday { get; set; }
    public bool Sunday { get; set; }

    public bool CrossesMidnight { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Employees currently assigned. Shown so a shift is not edited blind.</summary>
    public int AssignedCount { get; set; }

    /// <summary>Paid minutes implied by the times and break — computed, never entered.</summary>
    public int ExpectedMinutes
    {
        get
        {
            var span = CrossesMidnight
                ? (TimeSpan.FromHours(24) - StartTime.ToTimeSpan()) + EndTime.ToTimeSpan()
                : EndTime.ToTimeSpan() - StartTime.ToTimeSpan();

            return Math.Max((int)span.TotalMinutes - BreakMinutes, 0);
        }
    }

    /// <summary>Sunday-first mask, the form the database stores.</summary>
    public string ToWorkingDaysMask() =>
        new([
            Sunday ? 'Y' : 'N', Monday ? 'Y' : 'N', Tuesday ? 'Y' : 'N', Wednesday ? 'Y' : 'N',
            Thursday ? 'Y' : 'N', Friday ? 'Y' : 'N', Saturday ? 'Y' : 'N'
        ]);

    public void ApplyWorkingDaysMask(string mask)
    {
        if (mask.Length != 7)
        {
            return;
        }

        Sunday = char.ToUpperInvariant(mask[0]) == 'Y';
        Monday = char.ToUpperInvariant(mask[1]) == 'Y';
        Tuesday = char.ToUpperInvariant(mask[2]) == 'Y';
        Wednesday = char.ToUpperInvariant(mask[3]) == 'Y';
        Thursday = char.ToUpperInvariant(mask[4]) == 'Y';
        Friday = char.ToUpperInvariant(mask[5]) == 'Y';
        Saturday = char.ToUpperInvariant(mask[6]) == 'Y';
    }
}

public sealed record ShiftAssignmentAdminDto(
    long Id,
    long EmployeeId,
    string EmployeeName,
    string? DepartmentCode,
    string ShiftCode,
    string ShiftName,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string? Note);

/// <summary>An employee the assignment picker may offer.</summary>
public sealed record AssignableEmployeeDto(long Id, string Name, string? DepartmentCode, string? CurrentShiftCode);

public sealed class AssignShiftRequest
{
    [Required, RegularExpression("^[a-z0-9-]+$")]
    public string TenantKey { get; set; } = string.Empty;

    [Required] public long EmployeeId { get; set; }
    [Required] public long ShiftDefinitionId { get; set; }

    public DateOnly EffectiveFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? EffectiveTo { get; set; }

    [StringLength(500)] public string? Note { get; set; }
}
