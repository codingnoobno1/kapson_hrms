namespace Vispl.HRManagement.BM.ITF.Shared;

/// <summary>
/// The mail settings in force right now, resolved from wherever they are configured.
/// </summary>
/// <param name="Enabled">
/// Off stops delivery without removing the configuration — what a test run or a restored
/// backup wants: no mail leaves, and the reason is a setting somebody can see.
/// </param>
/// <param name="ReceiverBaseUrl">
/// Blank turns the admin inbox off rather than pointing it at nothing, which is right once a
/// real provider is relaying: the mail left and there is no mailbox of ours to read.
/// </param>
/// <param name="FromDatabase">
/// True once somebody has saved from the portal. Shown there so it is obvious whether the
/// values on screen are stored or are still the ones the file shipped with.
/// </param>
public sealed record MailSettingsSnapshot(
    bool Enabled,
    string SenderBaseUrl,
    string? ReceiverBaseUrl,
    string PublicBaseUrl,
    string? ApiKey,
    int TimeoutSeconds,
    bool FromDatabase);

/// <summary>
/// Answers what the mail settings are, every time they are needed.
///
/// Read per call rather than bound once at startup: these are edited from the admin portal, and
/// a setting that needs a restart to take effect is a setting somebody will change, watch do
/// nothing, and change back.
/// </summary>
public interface IMailSettingsSource
{
    ValueTask<MailSettingsSnapshot> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Called after a save, so the next caller reads the new values and not the cache.</summary>
    void Invalidate();
}
