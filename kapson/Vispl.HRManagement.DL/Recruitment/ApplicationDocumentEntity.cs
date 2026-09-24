namespace Vispl.HRManagement.DL.Recruitment;

public sealed class ApplicationDocumentEntity
{
    public long Id { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public long ApplicationId { get; set; }

    /// <summary>"Resume", or a DocumentCategory master code for a required document.</summary>
    public string CategoryCode { get; set; } = string.Empty;

    /// <summary>Display only. Never used to build a path on disk.</summary>
    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>Relative path returned by IMediaStorage. Generated name, never client-supplied.</summary>
    public string StoragePath { get; set; } = string.Empty;

    /// <summary>PendingScan, Clean, or Infected. Not downloadable unless Clean.</summary>
    public string ScanState { get; set; } = "PendingScan";

    public DateTimeOffset UploadedUtc { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
