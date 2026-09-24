using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.DL.Admin;

public sealed class MailSettingsRepository(HrManagementDbContext dbContext) : IMailSettingsRepository
{
    public async Task<MailSettingsRecord?> GetAsync(CancellationToken cancellationToken = default)
    {
        // No filter and no key: there is one row by design. OrderBy keeps the answer stable if
        // one is ever inserted by hand.
        var entity = await dbContext.MailSettings
            .AsNoTracking()
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToRecord(entity);
    }

    public async Task UpsertAsync(
        MailSettingsRecord record,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MailSettings
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (entity is null)
        {
            entity = new MailSettingsEntity();
            dbContext.MailSettings.Add(entity);
        }

        entity.Enabled = record.Enabled;
        entity.SenderBaseUrl = record.SenderBaseUrl;
        entity.ReceiverBaseUrl = record.ReceiverBaseUrl;
        entity.PublicBaseUrl = record.PublicBaseUrl;
        entity.ProtectedApiKey = record.ProtectedApiKey;
        entity.TimeoutSeconds = record.TimeoutSeconds;
        entity.UpdatedUtc = record.UpdatedUtc;
        entity.UpdatedBy = record.UpdatedBy;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static MailSettingsRecord ToRecord(MailSettingsEntity entity) => new()
    {
        Id = entity.Id,
        Enabled = entity.Enabled,
        SenderBaseUrl = entity.SenderBaseUrl,
        ReceiverBaseUrl = entity.ReceiverBaseUrl,
        PublicBaseUrl = entity.PublicBaseUrl,
        ProtectedApiKey = entity.ProtectedApiKey,
        TimeoutSeconds = entity.TimeoutSeconds,
        UpdatedUtc = entity.UpdatedUtc,
        UpdatedBy = entity.UpdatedBy
    };
}
