using Vispl.HRManagement.Contracts.Mobile.Home;
using Vispl.HRManagement.Contracts.Mobile.Shared;

namespace Vispl.HRManagement.MobileApp.Services.Clients;

public sealed class HomeApi(HrApiClient client)
{
    public Task<ApiResult<HrHomeDto>> GetHrHomeAsync(CancellationToken cancellationToken = default) =>
        client.GetAsync<HrHomeDto>("api/mobile/v1/home/hr", cancellationToken: cancellationToken);

    public Task<ApiResult<CursorPage<NotificationListItemDto>>> GetNotificationsAsync(
        string? tab = null,
        bool unreadOnly = false,
        int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var parameters = new List<string> { $"pageSize={pageSize}" };

        if (!string.IsNullOrWhiteSpace(tab))
        {
            parameters.Add($"tab={Uri.EscapeDataString(tab)}");
        }

        if (unreadOnly)
        {
            parameters.Add("unreadOnly=true");
        }

        return client.GetAsync<CursorPage<NotificationListItemDto>>(
            $"api/mobile/v1/notifications?{string.Join('&', parameters)}",
            cancellationToken: cancellationToken);
    }

    public Task<ApiResult<MutationResultDto>> MarkAllReadAsync(CancellationToken cancellationToken = default) =>
        client.PostAsync<MutationResultDto>(
            "api/mobile/v1/notifications/read",
            new MarkNotificationsReadRequest { All = true },
            cancellationToken);
}
