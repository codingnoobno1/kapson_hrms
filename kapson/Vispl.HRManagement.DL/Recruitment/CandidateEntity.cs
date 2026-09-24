namespace Vispl.HRManagement.DL.Recruitment;

public sealed class CandidateEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;

    /// <summary>Identity key within a tenant. Stored lower-cased.</summary>
    public string Email { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? CurrentLocation { get; set; }
    public decimal TotalExperienceYears { get; set; }

    /// <summary>Set only when the candidate activates an account. Null means apply-only.</summary>
    public string? AccountId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
