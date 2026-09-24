using Microsoft.Extensions.Configuration;
using Vispl.HRManagement.DL.ITF.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class LocalMediaStorage : IMediaStorage
{
    private readonly string rootPath;

    public LocalMediaStorage(IConfiguration configuration)
    {
        var configuredPath = configuration["MediaStorage:RootPath"] ?? ".data/media";
        rootPath = Path.GetFullPath(Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(Directory.GetCurrentDirectory(), configuredPath));
        Directory.CreateDirectory(rootPath);
    }

    public async Task<string> SaveAsync(string tenantKey, string storedFileName, Stream content, CancellationToken cancellationToken = default)
    {
        var relativePath = Path.Combine(tenantKey, storedFileName);
        var destination = ResolveSafePath(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        await using var file = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        await content.CopyToAsync(file, cancellationToken);
        return relativePath.Replace('\\', '/');
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        Stream stream = new FileStream(ResolveSafePath(storagePath), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var target = ResolveSafePath(storagePath);
        if (File.Exists(target))
        {
            File.Delete(target);
        }

        return Task.CompletedTask;
    }

    private string ResolveSafePath(string relativePath)
    {
        var candidate = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!candidate.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid media storage path.");
        }
        return candidate;
    }
}
