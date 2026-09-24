using Microsoft.AspNetCore.Mvc;
using Vispl.HRManagement.Api.Shared;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Mobile.Bootstrap;
using Vispl.HRManagement.Contracts.Mobile.Configuration;
using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.Api.Shared.Controllers;

// MBK-002 -------------------------------------------------------------------

[Route("api/mobile/v1/session")]
public sealed class MobileSessionController(IMobileSessionService sessions) : MobileApiControllerBase
{
    [HttpGet("bootstrap")]
    public async Task<ActionResult<ApiEnvelope<MobileSessionDto>>> Bootstrap(CancellationToken ct) =>
        OkEnvelope(await sessions.BootstrapAsync(Caller, ct));

    [HttpPost("refresh")]
    public async Task<ActionResult<ApiEnvelope<MobileSessionDto>>> Refresh(CancellationToken ct) =>
        OkEnvelope(await sessions.RefreshAsync(Caller, ct));

    [HttpGet("offline-grant")]
    public async Task<ActionResult<ApiEnvelope<MobileOfflinePolicyDto>>> OfflineGrant(CancellationToken ct) =>
        OkEnvelope(await sessions.GetOfflineGrantAsync(Caller, ct));

    [HttpPost("logout")]
    public async Task<IActionResult> Logout([FromBody] LogoutRequest request, CancellationToken ct)
    {
        await sessions.LogoutAsync(Caller, request, ct);
        return NoContent();
    }
}

// MBK-003 -------------------------------------------------------------------

[Route("api/mobile/v1/devices")]
public sealed class MobileDevicesController(IMobileDeviceService devices) : MobileApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<RegisteredDeviceDto>>>> List(CancellationToken ct) =>
        OkEnvelope(await devices.ListAsync(Caller, ct));

    [HttpPost]
    public async Task<ActionResult<ApiEnvelope<RegisteredDeviceDto>>> Register(
        [FromBody] RegisterDeviceRequest request,
        CancellationToken ct) =>
        OkEnvelope(await devices.RegisterAsync(Caller, request, ct));

    [HttpPatch("{deviceId}")]
    public async Task<ActionResult<ApiEnvelope<RegisteredDeviceDto>>> Rename(
        string deviceId,
        [FromBody] RenameDeviceRequest request,
        CancellationToken ct) =>
        OkEnvelope(await devices.RenameAsync(Caller, deviceId, request.Name, ct));

    [HttpDelete("{deviceId}")]
    public async Task<IActionResult> Revoke(string deviceId, CancellationToken ct)
    {
        await devices.RevokeAsync(Caller, deviceId, ct);
        return NoContent();
    }

    [HttpPut("push-token")]
    public async Task<IActionResult> UpdatePushToken(
        [FromBody] UpdatePushTokenRequest request,
        CancellationToken ct)
    {
        await devices.UpdatePushTokenAsync(Caller, request, ct);
        return NoContent();
    }
}

// MBK-004 -------------------------------------------------------------------

[Route("api/mobile/v1/configuration")]
public sealed class MobileConfigurationController(IMobileConfigurationService configuration)
    : MobileApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<MobileConfigurationDto>>> Get(
        [FromQuery] long? knownVersion,
        CancellationToken ct)
    {
        var published = await configuration.GetPublishedAsync(Caller, knownVersion, ct);
        return OkEnvelope(published, published.Version);
    }
}

// MBK-006 -------------------------------------------------------------------

[Route("api/mobile/v1/home")]
public sealed class MobileHomeController(IMobileHomeService home) : MobileApiControllerBase
{
    [HttpGet("hr")]
    [RequirePermission("HOME.HR")]
    public async Task<ActionResult<ApiEnvelope<HrHomeDto>>> Hr(CancellationToken ct) =>
        OkEnvelope(await home.GetHrHomeAsync(Caller, ct));

    [HttpGet("employee")]
    public async Task<ActionResult<ApiEnvelope<EmployeeHomeDto>>> Employee(CancellationToken ct) =>
        OkEnvelope(await home.GetEmployeeHomeAsync(Caller, ct));

    [HttpGet("manager")]
    [RequirePermission("HOME.MANAGER")]
    public async Task<ActionResult<ApiEnvelope<ManagerHomeDto>>> Manager(CancellationToken ct) =>
        OkEnvelope(await home.GetManagerHomeAsync(Caller, ct));
}

// MBK-007 -------------------------------------------------------------------

[Route("api/mobile/v1/search")]
public sealed class MobileSearchController(IMobileSearchService search) : MobileApiControllerBase
{
    [HttpGet("suggest")]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<string>>>> Suggest(
        [FromQuery] string query,
        CancellationToken ct) =>
        OkEnvelope(await search.SuggestAsync(Caller, query, ct));

    // Named "request", not "query": a parameter called query makes ?query=term look like a
    // prefixed binding for the whole object, so the term bound nowhere and every search came
    // back empty. The caller-facing spelling stays ?query=term either way.
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<GlobalSearchResultDto>>> Search(
        [FromQuery] GlobalSearchQuery request,
        CancellationToken ct) =>
        OkEnvelope(await search.SearchAsync(Caller, request, ct));
}

// MBK-008 -------------------------------------------------------------------

[Route("api/mobile/v1/notifications")]
public sealed class MobileNotificationsController(IMobileNotificationService notifications)
    : MobileApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<CursorPage<NotificationListItemDto>>>> List(
        [FromQuery] NotificationListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await notifications.ListAsync(Caller, request, ct));

    [HttpGet("{notificationId}")]
    public async Task<ActionResult<ApiEnvelope<NotificationDetailDto>>> Get(
        string notificationId,
        CancellationToken ct) =>
        OkEnvelope(await notifications.GetAsync(Caller, notificationId, ct));

    [HttpPost("read")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> MarkRead(
        [FromBody] MarkNotificationsReadRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await notifications.MarkReadAsync(Caller, request, ct));

    [HttpDelete("{notificationId}")]
    public async Task<IActionResult> Delete(string notificationId, CancellationToken ct)
    {
        await notifications.DeleteAsync(Caller, notificationId, ct);
        return NoContent();
    }
}

// MBK-009 -------------------------------------------------------------------

[Route("api/mobile/v1/calendar")]
public sealed class MobileCalendarController(IMobileCalendarService calendar) : MobileApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<IReadOnlyList<CalendarEventDto>>>> Range(
        [FromQuery] CalendarRangeQuery query,
        CancellationToken ct) =>
        OkEnvelope(await calendar.GetRangeAsync(Caller, query, ct));

    [HttpGet("{eventId}")]
    public async Task<ActionResult<ApiEnvelope<CalendarEventDto>>> Get(
        string eventId,
        CancellationToken ct) =>
        OkEnvelope(await calendar.GetEventAsync(Caller, eventId, ct));
}

// MBK-010 -------------------------------------------------------------------

[Route("api/mobile/v1/tasks")]
public sealed class MobileTasksController(IMobileTaskService tasks) : MobileApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiEnvelope<CursorPage<TaskListItemDto>>>> List(
        [FromQuery] TaskListQuery request,
        CancellationToken ct) =>
        OkEnvelope(await tasks.ListAsync(Caller, request, ct));

    [HttpGet("{taskId}")]
    public async Task<ActionResult<ApiEnvelope<TaskDetailDto>>> Get(string taskId, CancellationToken ct) =>
        OkEnvelope(await tasks.GetAsync(Caller, taskId, ct));

    [HttpPost("{taskId}/complete")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Complete(
        string taskId,
        [FromBody] CompleteTaskRequest request,
        CancellationToken ct) =>
        MutationEnvelope(await tasks.CompleteAsync(Caller, taskId, request, ct));

    [HttpPost("{taskId}/comments")]
    public async Task<ActionResult<ApiEnvelope<MutationResultDto>>> Comment(
        string taskId,
        [FromBody] AddRequestCommentBody request,
        CancellationToken ct) =>
        MutationEnvelope(await tasks.CommentAsync(Caller, taskId, request.Comment, ct));
}
