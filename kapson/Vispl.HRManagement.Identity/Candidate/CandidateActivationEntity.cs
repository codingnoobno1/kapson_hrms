namespace Vispl.HRManagement.Identity.Candidate;

/// <summary>
/// A one-time link letting an applicant set a password and claim the candidate record their
/// application created. Only the SHA-256 hash of the token is stored, so a database leak does
/// not yield usable links. Requesting a new link invalidates any outstanding one.
/// </summary>
public sealed class CandidateActivationEntity
{
    public long Id { get; set; }

    /// <summary>Base64 of the SHA-256 hash of the raw token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public string TenantKey { get; set; } = string.Empty;

    /// <summary>recruitment.Candidates row this link activates.</summary>
    public long CandidateId { get; set; }

    /// <summary>Stored lower-cased; the address the link was issued for.</summary>
    public string Email { get; set; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? ConsumedUtc { get; set; }
    public DateTimeOffset? InvalidatedUtc { get; set; }

    public bool IsUsable(DateTimeOffset now) =>
        ConsumedUtc is null && InvalidatedUtc is null && ExpiresUtc > now;
}
