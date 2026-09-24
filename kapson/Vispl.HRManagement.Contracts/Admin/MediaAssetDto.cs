namespace Vispl.HRManagement.Contracts.Admin;

public sealed class MediaAssetDto
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public string PublicUrl { get; set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; set; }
}
