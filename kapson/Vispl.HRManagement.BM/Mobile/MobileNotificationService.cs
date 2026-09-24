using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Shared;
using Vispl.HRManagement.DL.ITF.Shared;
using Vispl.HRManagement.VO.Shared;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Every method is scoped to the caller's own inbox by recipient id from the token. There is
/// no route by which one person reads or clears another's notifications.
/// </summary>
public sealed class MobileNotificationService(INotificationRepository notifications) : IMobileNotificationService
{
    public async Task<CursorPage<NotificationListItemDto>> ListAsync(
        MobileCallerContext caller, NotificationListQuery query, CancellationToken ct)
    {
        var all = await notifications.QueryAsync(
            caller.TenantKey, caller.UserId, NormaliseTab(query.Tab), query.UnreadOnly, ct);

        if (!string.IsNullOrWhiteSpace(query.Query))
        {
            all = all
                .Where(item =>
                    item.Title.Contains(query.Query, StringComparison.OrdinalIgnoreCase) ||
                    item.Body.Contains(query.Query, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var offset = DecodeCursor(query.Cursor);
        var page = all.Skip(offset).Take(pageSize).Select(Map).ToList();
        var next = offset + page.Count;
        var hasMore = next < all.Count;

        return new CursorPage<NotificationListItemDto>(
            page, hasMore ? EncodeCursor(next) : null, hasMore, pageSize, all.Count);
    }

    public async Task<NotificationDetailDto> GetAsync(
        MobileCallerContext caller, string notificationId, CancellationToken ct)
    {
        var notification = await notifications.GetAsync(
            caller.TenantKey, caller.UserId, ParseRequiredId(notificationId), ct)
            ?? throw new MobileNotFoundException("That notification is no longer available.");

        // Opening one marks it read. A separate call would leave the badge wrong for as long
        // as the client forgot to make it.
        if (!notification.IsRead)
        {
            await notifications.MarkReadAsync(
                caller.TenantKey, caller.UserId, [notification.Id], all: false, ct);
        }

        return new NotificationDetailDto(
            notification.Id.ToString(),
            notification.Category,
            notification.Title,
            notification.Body,
            notification.OccurredUtc,
            true,
            notification.ActionRoute is { Length: > 0 }
                ? [new ActionAvailabilityDto("open", true, null, RequiresOnline: true)]
                : []);
    }

    public async Task<MutationResultDto> MarkReadAsync(
        MobileCallerContext caller, MarkNotificationsReadRequest request, CancellationToken ct)
    {
        if (!request.All && request.NotificationIds.Count == 0)
        {
            throw new MobileValidationException(
                "NOTHING_SPECIFIED", "Name the notifications to mark read, or set all.");
        }

        var ids = request.NotificationIds
            .Select(id => long.TryParse(id, out var parsed) ? parsed : (long?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        var affected = await notifications.MarkReadAsync(
            caller.TenantKey, caller.UserId, ids, request.All, ct);

        var remaining = await notifications.CountUnreadAsync(caller.TenantKey, caller.UserId, ct);

        return new MutationResultDto(
            affected.ToString(), "Read", null, DateTimeOffset.UtcNow,
            $"{affected} marked read, {remaining} unread remaining.");
    }

    public Task DeleteAsync(MobileCallerContext caller, string notificationId, CancellationToken ct) =>
        notifications.DeleteAsync(caller.TenantKey, caller.UserId, ParseRequiredId(notificationId), ct);

    /// <summary>
    /// The reference screen splits General from Interview Schedule, so "general" means
    /// everything that is not an interview rather than a category of its own.
    /// </summary>
    private static string? NormaliseTab(string? tab) => tab?.ToLowerInvariant() switch
    {
        null or "" or "general" or "all" => null,
        "interview" or "interviews" => "Interview",
        var other => char.ToUpperInvariant(other[0]) + other[1..]
    };

    private static NotificationListItemDto Map(NotificationRecord item) => new(
        item.Id.ToString(),
        item.Category,
        item.Title,
        Preview(item.Body),
        item.Priority,
        item.IsRead,
        item.OccurredUtc,
        item.ActionRoute);

    /// <summary>The card shows two or three lines, so the list does not carry full bodies.</summary>
    private static string Preview(string body) =>
        body.Length <= 160 ? body : body[..157].TrimEnd() + "…";

    private static long ParseRequiredId(string value) =>
        long.TryParse(value, out var parsed)
            ? parsed
            : throw new MobileNotFoundException("That identifier is not valid.");

    private static int DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return 0;
        }

        try
        {
            return int.TryParse(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(cursor)), out var offset)
                ? Math.Max(0, offset)
                : 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    private static string EncodeCursor(int offset) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(offset.ToString()));
}
