using Vispl.HRManagement.Contracts.Recruitment;

namespace Vispl.HRManagement.BM.Recruitment;

/// <summary>
/// Content-type, extension and magic-byte checks for candidate uploads. The endpoint is public,
/// so a declared content type is treated as a claim to be verified, never as a fact.
/// </summary>
internal static class ApplicationFileValidator
{
    private static readonly Dictionary<string, byte[][]> Signatures = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = [[0x25, 0x50, 0x44, 0x46]],                              // %PDF
        [".docx"] = [[0x50, 0x4B, 0x03, 0x04], [0x50, 0x4B, 0x05, 0x06]],   // zip container
        [".doc"] = [[0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1]],      // OLE compound file
        [".png"] = [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]],
        [".jpg"] = [[0xFF, 0xD8, 0xFF]],
        [".jpeg"] = [[0xFF, 0xD8, 0xFF]]
    };

    /// <summary>
    /// Throws when a file breaks a rule. Reads and rewinds the stream, so the caller can still
    /// persist it afterwards.
    /// </summary>
    public static async Task ValidateAsync(UploadedFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0)
        {
            throw new ApplicationRejected("FILE_EMPTY", $"{file.CategoryCode}: the file is empty.");
        }

        if (file.Length > ApplicationDocumentRules.MaximumFileBytes)
        {
            throw new ApplicationRejected(
                "FILE_TOO_LARGE",
                $"{file.CategoryCode}: files must be {ApplicationDocumentRules.MaximumFileBytes / (1024 * 1024)} MB or smaller.");
        }

        if (!ApplicationDocumentRules.IsAllowed(file.ContentType, file.FileName))
        {
            throw new ApplicationRejected(
                "FILE_TYPE_NOT_ALLOWED",
                $"{file.CategoryCode}: only PDF, DOC, DOCX, PNG and JPEG files are accepted.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!Signatures.TryGetValue(extension, out var candidates))
        {
            throw new ApplicationRejected("FILE_TYPE_NOT_ALLOWED", $"{file.CategoryCode}: unsupported file type.");
        }

        var longest = candidates.Max(signature => signature.Length);
        var header = new byte[longest];
        var read = await ReadHeaderAsync(file.Content, header, cancellationToken);

        var matches = candidates.Any(signature =>
            read >= signature.Length && header.Take(signature.Length).SequenceEqual(signature));

        if (!matches)
        {
            // The extension says one thing and the bytes say another.
            throw new ApplicationRejected(
                "FILE_CONTENT_MISMATCH",
                $"{file.CategoryCode}: the file contents do not match its {extension} extension.");
        }
    }

    private static async Task<int> ReadHeaderAsync(Stream stream, byte[] buffer, CancellationToken cancellationToken)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, cancellationToken);

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return read;
    }
}

/// <summary>Internal signal mapped onto ApplicationRejectedException by the service.</summary>
internal sealed class ApplicationRejected(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
