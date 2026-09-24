namespace Vispl.HRManagement.DL.Admin;

public sealed class MediaAssetEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string StoragePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long Length { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
