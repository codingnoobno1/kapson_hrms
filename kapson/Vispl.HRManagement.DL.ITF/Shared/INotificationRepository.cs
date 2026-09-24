using Vispl.HRManagement.VO.Shared;

namespace Vispl.HRManagement.DL.ITF.Shared;

public interface INotificationRepository
{
    Task<IReadOnlyList<NotificationRecord>> QueryAsync(
        string tenantKey, string recipientId, string? category, bool unreadOnly,
        CancellationToken cancellationToken = default);

    Task<NotificationRecord?> GetAsync(
        string tenantKey, string recipientId, long notificationId, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(
        string tenantKey, string recipientId, CancellationToken cancellationToken = default);

    Task<long> AddAsync(NotificationRecord notification, CancellationToken cancellationToken = default);

    /// <summary>True when this recipient already has a live notification for the same subject.</summary>
    Task<bool> ExistsForSubjectAsync(
        string tenantKey, string recipientId, string subjectType, string subjectId,
        CancellationToken cancellationToken = default);

    /// <summary>Marks the given ids read, or every unread one when ids is empty. Returns the count.</summary>
    Task<int> MarkReadAsync(
        string tenantKey, string recipientId, IReadOnlyList<long> notificationIds, bool all,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string tenantKey, string recipientId, long notificationId, CancellationToken cancellationToken = default);
}
