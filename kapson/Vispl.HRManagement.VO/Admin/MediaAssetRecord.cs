namespace Vispl.HRManagement.VO.Admin;

public sealed class MediaAssetRecord
{
    public long Id { get; set; }
    public required string TenantKey { get; set; }
    public required string OriginalFileName { get; set; }
    public required string StoredFileName { get; set; }
    public required string StoragePath { get; set; }
    public required string ContentType { get; set; }
    public long Length { get; set; }
    public DateTimeOffset CreatedUtc { get; set; }
}
