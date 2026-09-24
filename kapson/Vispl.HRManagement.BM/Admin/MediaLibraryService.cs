using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.BM.Admin;

public sealed class MediaLibraryService(IMediaAssetRepository repository, IMediaStorage storage) : IMediaLibraryService
{
    private const long MaximumLength = 10 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> AllowedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png", ["image/jpeg"] = ".jpg", ["image/webp"] = ".webp", ["image/gif"] = ".gif", ["image/svg+xml"] = ".svg"
    };

    public async Task<IReadOnlyList<MediaAssetDto>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default) =>
        (await repository.GetAllAsync(Normalize(tenantKey), cancellationToken)).Select(ToDto).ToList();

    public async Task<MediaAssetDto> UploadAsync(string tenantKey, string originalFileName, string contentType, long length, Stream content, CancellationToken cancellationToken = default)
    {
        if (!AllowedTypes.TryGetValue(contentType, out var extension)) throw new InvalidOperationException("Only PNG, JPEG, WebP, GIF and SVG images are supported.");
        if (length <= 0 || length > MaximumLength) throw new InvalidOperationException("Image size must be between 1 byte and 10 MB.");
        var normalizedTenant = Normalize(tenantKey);
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var storagePath = await storage.SaveAsync(normalizedTenant, storedFileName, content, cancellationToken);
        var record = new MediaAssetRecord
        {
            TenantKey = normalizedTenant, OriginalFileName = Path.GetFileName(originalFileName), StoredFileName = storedFileName,
            StoragePath = storagePath, ContentType = contentType, Length = length, CreatedUtc = DateTimeOffset.UtcNow
        };
        record.Id = await repository.AddAsync(record, cancellationToken);
        return ToDto(record);
    }

    public async Task<MediaFile?> OpenReadAsync(long id, CancellationToken cancellationToken = default)
    {
        var asset = await repository.GetByIdAsync(id, cancellationToken);
        return asset is null ? null : new MediaFile(await storage.OpenReadAsync(asset.StoragePath, cancellationToken), asset.ContentType, asset.OriginalFileName);
    }

    private static MediaAssetDto ToDto(MediaAssetRecord item) => new()
    {
        Id = item.Id, TenantKey = item.TenantKey, OriginalFileName = item.OriginalFileName,
        ContentType = item.ContentType, Length = item.Length, PublicUrl = $"/media/{item.Id}", CreatedUtc = item.CreatedUtc
    };
    private static string Normalize(string value) { ArgumentException.ThrowIfNullOrWhiteSpace(value); return value.Trim().ToLowerInvariant(); }
}
