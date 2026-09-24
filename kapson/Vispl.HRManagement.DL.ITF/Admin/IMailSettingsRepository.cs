using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.ITF.Admin;

/// <summary>
/// The one mail settings row, or nothing at all.
///
/// Nothing is what a fresh installation has, and it is not an error: the values in
/// configuration are what runs until somebody saves over them from the portal.
/// </summary>
public interface IMailSettingsRepository
{
    Task<MailSettingsRecord?> GetAsync(CancellationToken cancellationToken = default);

    Task UpsertAsync(MailSettingsRecord record, CancellationToken cancellationToken = default);
}
