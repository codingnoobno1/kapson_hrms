using Vispl.HRManagement.Contracts.Mobile.Bootstrap;
using Vispl.HRManagement.Contracts.Mobile.Configuration;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.BM.ITF.Mobile;

public interface IMobileSessionService
{
    Task<MobileSessionDto> BootstrapAsync(MobileCallerContext caller, CancellationToken ct);

    Task<MobileSessionDto> RefreshAsync(MobileCallerContext caller, CancellationToken ct);

    Task LogoutAsync(MobileCallerContext caller, LogoutRequest request, CancellationToken ct);

    Task<MobileOfflinePolicyDto> GetOfflineGrantAsync(MobileCallerContext caller, CancellationToken ct);
}

public interface IMobileDeviceService
{
    Task<IReadOnlyList<RegisteredDeviceDto>> ListAsync(MobileCallerContext caller, CancellationToken ct);

    Task<RegisteredDeviceDto> RegisterAsync(
        MobileCallerContext caller,
        RegisterDeviceRequest request,
        CancellationToken ct);

    Task<RegisteredDeviceDto> RenameAsync(
        MobileCallerContext caller,
        string deviceId,
        string name,
        CancellationToken ct);

    Task RevokeAsync(MobileCallerContext caller, string deviceId, CancellationToken ct);

    Task UpdatePushTokenAsync(
        MobileCallerContext caller,
        UpdatePushTokenRequest request,
        CancellationToken ct);
}

public interface IMobileConfigurationService
{
    Task<MobileConfigurationDto> GetPublishedAsync(
        MobileCallerContext caller,
        long? knownVersion,
        CancellationToken ct);
}

public interface IMobileHomeService
{
    Task<HrHomeDto> GetHrHomeAsync(MobileCallerContext caller, CancellationToken ct);

    Task<EmployeeHomeDto> GetEmployeeHomeAsync(MobileCallerContext caller, CancellationToken ct);

    Task<ManagerHomeDto> GetManagerHomeAsync(MobileCallerContext caller, CancellationToken ct);
}

public interface IMobileSearchService
{
    Task<IReadOnlyList<string>> SuggestAsync(
        MobileCallerContext caller,
        string query,
        CancellationToken ct);

    Task<GlobalSearchResultDto> SearchAsync(
        MobileCallerContext caller,
        GlobalSearchQuery query,
        CancellationToken ct);
}

public interface IMobileNotificationService
{
    Task<CursorPage<NotificationListItemDto>> ListAsync(
        MobileCallerContext caller,
        NotificationListQuery query,
        CancellationToken ct);

    Task<NotificationDetailDto> GetAsync(
        MobileCallerContext caller,
        string notificationId,
        CancellationToken ct);

    Task<MutationResultDto> MarkReadAsync(
        MobileCallerContext caller,
        MarkNotificationsReadRequest request,
        CancellationToken ct);

    Task DeleteAsync(MobileCallerContext caller, string notificationId, CancellationToken ct);
}

public interface IMobileCalendarService
{
    Task<IReadOnlyList<CalendarEventDto>> GetRangeAsync(
        MobileCallerContext caller,
        CalendarRangeQuery query,
        CancellationToken ct);

    Task<CalendarEventDto> GetEventAsync(
        MobileCallerContext caller,
        string eventId,
        CancellationToken ct);
}

public interface IMobileTaskService
{
    Task<CursorPage<TaskListItemDto>> ListAsync(
        MobileCallerContext caller,
        TaskListQuery query,
        CancellationToken ct);

    Task<TaskDetailDto> GetAsync(MobileCallerContext caller, string taskId, CancellationToken ct);

    Task<MutationResultDto> CompleteAsync(
        MobileCallerContext caller,
        string taskId,
        CompleteTaskRequest request,
        CancellationToken ct);

    Task<MutationResultDto> CommentAsync(
        MobileCallerContext caller,
        string taskId,
        string comment,
        CancellationToken ct);
}
