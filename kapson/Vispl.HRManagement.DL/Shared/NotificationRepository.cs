using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Shared;
using Vispl.HRManagement.VO.Shared;

namespace Vispl.HRManagement.DL.Shared;

public sealed class NotificationRepository(HrManagementDbContext dbContext) : INotificationRepository
{
    public async Task<IReadOnlyList<NotificationRecord>> QueryAsync(
        string tenantKey, string recipientId, string? category, bool unreadOnly,
        CancellationToken cancellationToken = default)
    {
        var query = Live(tenantKey, recipientId);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => item.Category == category);
        }

        if (unreadOnly)
        {
            query = query.Where(item => item.ReadUtc == null);
        }

        var rows = await query
            .OrderByDescending(item => item.OccurredUtc)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<NotificationRecord?> GetAsync(
        string tenantKey, string recipientId, long notificationId, CancellationToken cancellationToken = default)
    {
        var entity = await Live(tenantKey, recipientId)
            .FirstOrDefaultAsync(item => item.Id == notificationId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public Task<int> CountUnreadAsync(
        string tenantKey, string recipientId, CancellationToken cancellationToken = default) =>
        Live(tenantKey, recipientId).CountAsync(item => item.ReadUtc == null, cancellationToken);

    public async Task<long> AddAsync(
        NotificationRecord notification, CancellationToken cancellationToken = default)
    {
        var entity = new NotificationEntity
        {
            TenantKey = notification.TenantKey,
            RecipientId = notification.RecipientId,
            Category = notification.Category,
            Title = notification.Title,
            Body = notification.Body,
            Priority = notification.Priority,
            ActionRoute = notification.ActionRoute,
            SubjectType = notification.SubjectType,
            SubjectId = notification.SubjectId,
            OccurredUtc = notification.OccurredUtc
        };

        dbContext.Notifications.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public Task<bool> ExistsForSubjectAsync(
        string tenantKey, string recipientId, string subjectType, string subjectId,
        CancellationToken cancellationToken = default) =>
        Live(tenantKey, recipientId).AnyAsync(
            item => item.SubjectType == subjectType && item.SubjectId == subjectId, cancellationToken);

    public async Task<int> MarkReadAsync(
        string tenantKey, string recipientId, IReadOnlyList<long> notificationIds, bool all,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Notifications
            .Where(item => item.TenantKey == tenantKey &&
                           item.RecipientId == recipientId &&
                           item.DeletedUtc == null &&
                           item.ReadUtc == null);

        if (!all)
        {
            if (notificationIds.Count == 0)
            {
                return 0;
            }

            query = query.Where(item => notificationIds.Contains(item.Id));
        }

        var now = DateTimeOffset.UtcNow;
        var rows = await query.ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.ReadUtc = now;
        }

        if (rows.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return rows.Count;
    }

    public async Task DeleteAsync(
        string tenantKey, string recipientId, long notificationId, CancellationToken cancellationToken = default)
    {
        var entity = await Live(tenantKey, recipientId)
            .FirstOrDefaultAsync(item => item.Id == notificationId, cancellationToken);

        if (entity is null)
        {
            return;
        }

        // Soft delete: the row stays so an audit of what was sent survives the recipient
        // clearing their inbox.
        entity.DeletedUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Notifications this recipient can still see: theirs, and not deleted.</summary>
    private IQueryable<NotificationEntity> Live(string tenantKey, string recipientId) =>
        dbContext.Notifications
            .Where(item => item.TenantKey == tenantKey &&
                           item.RecipientId == recipientId &&
                           item.DeletedUtc == null);

    private static NotificationRecord Map(NotificationEntity item) => new(
        item.Id, item.TenantKey, item.RecipientId, item.Category, item.Title, item.Body,
        item.Priority, item.ActionRoute, item.SubjectType, item.SubjectId,
        item.OccurredUtc, item.ReadUtc);
}
