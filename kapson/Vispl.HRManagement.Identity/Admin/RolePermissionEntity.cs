namespace Vispl.HRManagement.Identity.Admin;

public sealed class RolePermissionEntity
{
    public long Id { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public string PermissionKey { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
    public HrRole Role { get; set; } = null!;
}
