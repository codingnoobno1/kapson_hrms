using Microsoft.AspNetCore.SignalR.Client;

namespace Vispl.HRManagement.MobileApp.Services;

public sealed record HrDataChanged(string Topic, string Path, DateTimeOffset ChangedUtc);
public enum HrRealtimeState { Offline, Connecting, Live, Reconnecting }

public sealed class HrRealtimeClient(
    AppSession session,
    ITokenStore tokens,
    MobileAppOptions options,
    ILogger<HrRealtimeClient> logger) : IAsyncDisposable
{
    private HubConnection? connection;
    private readonly SemaphoreSlim gate = new(1, 1);

    public HrRealtimeState State { get; private set; } = HrRealtimeState.Offline;
    public event Action<HrDataChanged>? DataChanged;
    public event Action? StateChanged;

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (!session.IsSignedIn || connection?.State == HubConnectionState.Connected) return;
        await gate.WaitAsync(ct);
        try
        {
            if (!session.IsSignedIn || connection?.State == HubConnectionState.Connected) return;
            if (connection is not null) await connection.DisposeAsync();
            SetState(HrRealtimeState.Connecting);

            var root = session.GetBaseAddress(options).TrimEnd('/') + "/";
            connection = new HubConnectionBuilder()
                .WithUrl(new Uri(new Uri(root), "hubs/hr"), channel =>
                    channel.AccessTokenProvider = () => tokens.GetAccessTokenAsync())
                .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)])
                .Build();

            connection.On<HrDataChanged>("DataChanged", change => DataChanged?.Invoke(change));
            connection.Reconnecting += _ => { SetState(HrRealtimeState.Reconnecting); return Task.CompletedTask; };
            connection.Reconnected += _ => { SetState(HrRealtimeState.Live); return Task.CompletedTask; };
            connection.Closed += _ => { SetState(HrRealtimeState.Offline); return Task.CompletedTask; };
            await connection.StartAsync(ct);
            SetState(HrRealtimeState.Live);
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            logger.LogInformation(exception, "Live updates are unavailable; pull-to-refresh remains active.");
            SetState(HrRealtimeState.Offline);
        }
        finally { gate.Release(); }
    }

    public async Task StopAsync()
    {
        if (connection is not null) await connection.StopAsync();
        SetState(HrRealtimeState.Offline);
    }

    private void SetState(HrRealtimeState state)
    {
        if (State == state) return;
        State = state;
        StateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null) await connection.DisposeAsync();
        gate.Dispose();
    }
}
