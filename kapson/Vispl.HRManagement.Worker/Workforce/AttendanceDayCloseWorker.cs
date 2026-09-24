using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Workforce;

namespace Vispl.HRManagement.Worker.Workforce;

/// <summary>
/// Closes finished attendance days for every live client.
///
/// Not turning up produces no punch and no correction, so nothing else in the system ever
/// writes that day. Without this job an absence exists only as an inference on a dashboard,
/// which is why the tile and the month overview used to disagree about the same fact.
///
/// It runs on a plain interval rather than at a fixed hour because clients sit in different
/// time zones: each one's own clock decides which of its days are over, and closing a day that
/// is already closed does nothing.
/// </summary>
public sealed class AttendanceDayCloseWorker(
    IServiceScopeFactory scopes,
    ILogger<AttendanceDayCloseWorker> logger) : BackgroundService
{
    /// <summary>
    /// Every day in this window is checked on each pass, so a server that was down over a
    /// weekend catches up instead of leaving a hole in the register.
    /// </summary>
    private const int LookbackDays = 14;

    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    /// <summary>
    /// Long enough for the host to finish starting and short enough that a fresh deployment
    /// closes yesterday without waiting an hour.
    /// </summary>
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

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
            var closer = scope.ServiceProvider.GetRequiredService<IAttendanceDayCloser>();

            var clients = await tenants.GetAllAsync(ct);

            foreach (var client in clients.Where(item => item.IsActive))
            {
                // One client's bad data must not stop the rest from being closed.
                try
                {
                    var closed = await closer.CatchUpAsync(client.TenantKey, LookbackDays, ct);

                    foreach (var day in closed)
                    {
                        logger.LogInformation(
                            "Closed {Date:yyyy-MM-dd} for {Tenant}: {Absent} absent, {Leave} on leave, " +
                            "{Rest} not rostered, {Recorded} already recorded of {Population}.",
                            day.Date, day.TenantKey, day.MarkedAbsent, day.MarkedOnLeave,
                            day.MarkedRestDay, day.AlreadyRecorded, day.Population);
                    }
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    logger.LogError(exception,
                        "Could not close attendance days for {Tenant}.", client.TenantKey);
                }
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "The attendance day-close pass failed.");
        }
    }
}
