namespace Vispl.HRManagement.DL.Admin;

public sealed class TenantEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? PrimaryDomain { get; set; }
    /// <summary>
    /// The zone this client's calendar days are measured in. A gate pass valid "on the 4th" is
    /// valid on the 4th where the gate is, not where the server is — in IST those differ for the
    /// first five and a half hours of every day.
    /// </summary>
    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Report Configuration's "Retirement Date": age this client retires staff at.</summary>
    public int RetirementAgeYears { get; set; } = 58;

    /// <summary>
    /// Whether this client may use the HR web portal at all.
    ///
    /// Defaults to true so existing clients are unaffected. Turning it off closes the portal for
    /// the whole client — including sessions already open — without touching a single account,
    /// which is what makes it usable for a contract lapse or a security incident.
    /// </summary>
    public bool HrPortalEnabled { get; set; } = true;

    public bool IsActive { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
