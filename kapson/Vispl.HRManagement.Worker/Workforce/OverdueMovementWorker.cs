using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Workforce;

namespace Vispl.HRManagement.Worker.Workforce;

/// <summary>
/// Reports people who are out and overdue, for every live client.
///
/// Unlike every other movement notification this one has no gesture behind it: the employee not
/// coming back is precisely what has to be noticed, so it can only be found by looking.
///
/// It runs often, because an overdue return is time-sensitive in a way a closed attendance day
/// is not. Repeating a sweep is cheap and silent — the notification publisher keys each notice
/// to its movement, so a person still out an hour later is not announced twelve more times.
/// </summary>
public sealed class OverdueMovementWorker(
    IServiceScopeFactory scopes,
    ILogger<OverdueMovementWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Long enough for the host to finish starting, short enough that a restart does not leave
    /// someone unreported for a whole interval.
    /// </summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(45);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken);

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopes.CreateScope();

            var tenants = scope.ServiceProvider.GetRequiredService<ITenantService>();
            var notifier = scope.ServiceProvider.GetRequiredService<IOverdueMovementNotifier>();

            var clients = await tenants.GetAllAsync(ct);

            foreach (var client in clients.Where(item => item.IsActive))
            {
                // One client's bad data must not stop the rest from being swept.
                try
                {
                    var result = await notifier.SweepAsync(client.TenantKey, ct);

                    if (result.Notified > 0)
                    {
                        logger.LogInformation(
                            "{Tenant}: {Overdue} overdue movement(s), {Notified} reported.",
                            result.TenantKey, result.OverdueFound, result.Notified);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception,
                        "Could not sweep overdue movements for {Tenant}.", client.TenantKey);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The overdue movement sweep failed.");
        }
    }
}
