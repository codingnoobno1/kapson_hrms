using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

public interface IMediaLibraryService
{
    Task<IReadOnlyList<MediaAssetDto>> GetAllAsync(string tenantKey, CancellationToken cancellationToken = default);
    Task<MediaAssetDto> UploadAsync(string tenantKey, string originalFileName, string contentType, long length, Stream content, CancellationToken cancellationToken = default);
    Task<MediaFile?> OpenReadAsync(long id, CancellationToken cancellationToken = default);
}

public sealed record MediaFile(Stream Content, string ContentType, string FileName);
