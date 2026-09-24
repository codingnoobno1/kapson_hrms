namespace Vispl.HRManagement.DL.Admin;

public sealed class CareerSiteEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string DraftJson { get; set; } = string.Empty;
    public string? PublishedJson { get; set; }
    public int PublishedVersion { get; set; }
    public DateTimeOffset? PublishedUtc { get; set; }
    public DateTimeOffset UpdatedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
