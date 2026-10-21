using Microsoft.AspNetCore.SignalR.Client;
using Vispl.HRManagement.Mobile.Client.Auth;
using Vispl.HRManagement.Mobile.Client.Http;

namespace Vispl.HRManagement.Mobile.Client.Realtime;

public sealed record MobileDataChanged(string Topic, string Path, DateTimeOffset ChangedUtc);
public enum MobileRealtimeState { Offline, Connecting, Live, Reconnecting }

/// <summary>One resilient, authenticated live channel shared by every employee-app screen.</summary>
public sealed class MobileRealtimeClient(MobileApiOptions options, MobileSession session) : IAsyncDisposable
{
    private HubConnection? connection;
    private readonly SemaphoreSlim gate = new(1, 1);

    public MobileRealtimeState State { get; private set; } = MobileRealtimeState.Offline;
    public event EventHandler<MobileDataChanged>? DataChanged;
    public event EventHandler<MobileRealtimeState>? StateChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!session.IsSignedIn || connection?.State == HubConnectionState.Connected) return;
        await gate.WaitAsync(cancellationToken);
        try
        {
            if (!session.IsSignedIn || connection?.State == HubConnectionState.Connected) return;
            await DisposeConnectionAsync();
            SetState(MobileRealtimeState.Connecting);

            connection = new HubConnectionBuilder()
                .WithUrl(options.ResolveUri("hubs/hr"), transport =>
                {
                    transport.AccessTokenProvider = () => Task.FromResult(session.Tokens?.AccessToken);
                    transport.Headers["ngrok-skip-browser-warning"] = "true";
                })
                .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)])
                .Build();

            connection.On<MobileDataChanged>("DataChanged", change => DataChanged?.Invoke(this, change));
            connection.Reconnecting += _ => { SetState(MobileRealtimeState.Reconnecting); return Task.CompletedTask; };
            connection.Reconnected += _ => { SetState(MobileRealtimeState.Live); return Task.CompletedTask; };
            connection.Closed += _ => { SetState(MobileRealtimeState.Offline); return Task.CompletedTask; };
            await connection.StartAsync(cancellationToken);
            SetState(MobileRealtimeState.Live);
        }
        catch
        {
            SetState(MobileRealtimeState.Offline);
            // Live sync is an enhancement. Normal HTTP refresh remains available offline.
        }
        finally { gate.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (connection is not null) await connection.StopAsync(cancellationToken);
        SetState(MobileRealtimeState.Offline);
    }

    private void SetState(MobileRealtimeState value)
    {
        if (State == value) return;
        State = value;
        StateChanged?.Invoke(this, value);
    }

    private async Task DisposeConnectionAsync()
    {
        if (connection is null) return;
        await connection.DisposeAsync();
        connection = null;
    }

    public async ValueTask DisposeAsync()
    {
        await DisposeConnectionAsync();
        gate.Dispose();
    }
}
