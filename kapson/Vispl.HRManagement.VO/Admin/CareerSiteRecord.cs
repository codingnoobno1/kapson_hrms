namespace Vispl.HRManagement.VO.Admin;

public sealed class CareerSiteRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public required string DraftJson { get; set; }
    public string? PublishedJson { get; set; }
    public int PublishedVersion { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
}
