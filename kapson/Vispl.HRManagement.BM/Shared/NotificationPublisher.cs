using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.DL.ITF.Shared;
using Vispl.HRManagement.VO.Shared;

namespace Vispl.HRManagement.BM.Shared;

public sealed class NotificationPublisher(
    INotificationRepository notifications,
    IPermissionAudience audience) : INotificationPublisher
{
    public async Task PublishAsync(
        NotificationRequest request, IReadOnlyList<string> recipientIds, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        foreach (var recipientId in recipientIds.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            // Fan-out is per recipient so each person can read or clear their own copy.
            // A repeat for the same subject is skipped rather than stacking duplicates in
            // an inbox, which is what makes a notification list unusable.
            if (request.SubjectType is { Length: > 0 } subjectType &&
                request.SubjectId is { Length: > 0 } subjectId &&
                await notifications.ExistsForSubjectAsync(
                    request.TenantKey, recipientId, subjectType, subjectId, cancellationToken))
            {
                continue;
            }

            await notifications.AddAsync(
                new NotificationRecord(
                    0, request.TenantKey, recipientId, request.Category, request.Title, request.Body,
                    request.Priority, request.ActionRoute, request.SubjectType, request.SubjectId,
                    now, null),
                cancellationToken);
        }
    }

    public async Task PublishToPermissionAsync(
        NotificationRequest request, string permission, CancellationToken cancellationToken = default)
    {
        var recipients = await audience.GetAccountIdsWithPermissionAsync(
            request.TenantKey, permission, cancellationToken);

        await PublishAsync(request, recipients, cancellationToken);
    }
}
