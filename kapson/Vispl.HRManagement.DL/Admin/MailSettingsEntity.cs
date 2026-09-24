namespace Vispl.HRManagement.DL.Admin;

public sealed class MailSettingsEntity
{
    public long Id { get; set; }
    public bool Enabled { get; set; } = true;
    public string SenderBaseUrl { get; set; } = string.Empty;
    public string? ReceiverBaseUrl { get; set; }
    public string PublicBaseUrl { get; set; } = string.Empty;
    public string? ProtectedApiKey { get; set; }
    public int TimeoutSeconds { get; set; } = 15;
    public DateTimeOffset UpdatedUtc { get; set; }
    public string? UpdatedBy { get; set; }
    public byte[] RowVersion { get; set; } = [];
}
