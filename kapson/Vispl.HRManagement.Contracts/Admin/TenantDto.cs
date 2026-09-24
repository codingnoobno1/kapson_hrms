using System.ComponentModel.DataAnnotations;

namespace Vispl.HRManagement.Contracts.Admin;

public sealed class TenantDto
{
    public long Id { get; set; }
    [Required, RegularExpression("^[a-z0-9-]+$")] public string TenantKey { get; set; } = string.Empty;
    [Required, StringLength(160)] public string DisplayName { get; set; } = string.Empty;
    [StringLength(240)] public string? PrimaryDomain { get; set; }
    /// <summary>
    /// IANA or Windows zone id. Calendar-day decisions — a gate pass valid "on the 4th" — are
    /// resolved here rather than in UTC, which would be wrong by hours for most of the world.
    /// </summary>
    [Required, StringLength(80)]
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Report Configuration's "Retirement Date": age this client retires staff at.</summary>
    [Range(40, 75)]
    public int RetirementAgeYears { get; set; } = 58;

    /// <summary>
    /// Whether this client may sign in to the HR web portal. Independent of
    /// <see cref="IsActive"/>: a live client may have the portal switched off while its
    /// recruitment site and mobile apps keep working.
    /// </summary>
    public bool HrPortalEnabled { get; set; } = true;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
