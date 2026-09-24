using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

/// <summary>
/// Reads and writes the mail configuration from the admin portal.
///
/// Two halves that live in two places, and the split is deliberate. What this application needs
/// in order to reach the mail services is stored here. How mail actually leaves the building —
/// the relay, its credentials, the from-address — belongs to the sender service, which owns it
/// so that service stays liftable. This talks to both so an administrator sees one page.
/// </summary>
public interface IMailConfigurationService
{
    Task<MailConfigurationDto> GetAsync(CancellationToken cancellationToken = default);

    Task<MailConfigurationDto> SaveAsync(
        SaveMailConfigurationRequest request,
        string changedBy,
        CancellationToken cancellationToken = default);

    /// <summary>Null when the sender cannot be reached. Not an error — it may simply be down.</summary>
    Task<MailRelayDto?> GetRelayAsync(CancellationToken cancellationToken = default);

    Task<MailConfigurationResult> SaveRelayAsync(
        SaveMailRelayRequest request,
        string changedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends one message to an address the administrator chooses. Configuration that has not
    /// delivered anything is configuration nobody has any reason to trust.
    /// </summary>
    Task<MailConfigurationResult> SendTestAsync(
        string toAddress,
        string requestedBy,
        CancellationToken cancellationToken = default);
}
