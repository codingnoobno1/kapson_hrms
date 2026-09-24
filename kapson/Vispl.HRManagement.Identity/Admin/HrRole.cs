using Microsoft.AspNetCore.Identity;
using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.Identity.Admin;

public sealed class HrRole : IdentityRole
{
    public string? TenantKey { get; set; }
    public AccountPortal Portal { get; set; } = AccountPortal.Platform;
    public string RoleCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
