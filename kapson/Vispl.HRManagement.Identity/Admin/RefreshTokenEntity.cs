namespace Vispl.HRManagement.Identity.Admin;

/// <summary>
/// A refresh token issued to one device. The raw token is returned to the client once and
/// never stored; only its SHA-256 hash is kept, so a database leak does not yield usable
/// tokens. Tokens are single-use: redeeming one rotates it and records the successor.
/// </summary>
public sealed class RefreshTokenEntity
{
    public long Id { get; set; }

    /// <summary>Base64 of the SHA-256 hash of the raw token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public string UserId { get; set; } = string.Empty;
    public string? TenantKey { get; set; }

    /// <summary>Groups the rotation chain so revoking one link can revoke the whole family.</summary>
    public string FamilyId { get; set; } = string.Empty;

    public string? DeviceId { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset ExpiresUtc { get; set; }
    public DateTimeOffset? ConsumedUtc { get; set; }
    public DateTimeOffset? RevokedUtc { get; set; }
    public string? RevokedReason { get; set; }

    public bool IsActive(DateTimeOffset now) =>
        ConsumedUtc is null && RevokedUtc is null && ExpiresUtc > now;
}
