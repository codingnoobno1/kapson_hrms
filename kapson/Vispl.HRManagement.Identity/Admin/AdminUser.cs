using Microsoft.AspNetCore.Identity;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.Identity.Admin;

public sealed class AdminUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public string? TenantKey { get; set; }
    public AccountPortal Portal { get; set; } = AccountPortal.Platform;
    public string? EmployeeNumber { get; set; }

    /// <summary>
    /// recruitment.Candidates row this account belongs to, for Candidate portal users only.
    /// Held here so token issuance does not have to reach into the recruitment module.
    /// </summary>
    public long? CandidateId { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether this account may sign in to a mobile app.
    ///
    /// Separate from <see cref="IsActive"/> on purpose. Being able to use the web portal is not
    /// the same decision as being allowed to carry the company's attendance, leave and salary
    /// data around on a phone, and revoking one should not require revoking the other.
    ///
    /// Off by default, so a mobile app can never become a second, unreviewed door into an
    /// account somebody only meant to give web access to.
    /// </summary>
    public bool MobileAccessEnabled { get; set; }
    public bool MustChangePassword { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public DateTimeOffset? PasswordChangedUtc { get; set; }
    public DateTimeOffset? LastLoginUtc { get; set; }

    /// <summary>The sign-in before the current one — the HR header's "LAST LOGIN".</summary>
    public DateTimeOffset? PreviousLoginUtc { get; set; }
    public string? CreatedByUserId { get; set; }
}
