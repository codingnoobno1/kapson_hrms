namespace Vispl.HRManagement.DL.ITF.Admin;

public interface IMediaStorage
{
    Task<string> SaveAsync(string tenantKey, string storedFileName, Stream content, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default);

    /// <summary>Removes a stored blob. Silently succeeds when it is already gone.</summary>
    Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default);
}
