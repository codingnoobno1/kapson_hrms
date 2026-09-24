using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.BM.ITF.Workforce;
using Vispl.HRManagement.BM.Shared;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Workforce;

/// <summary>
/// Finds people who are still out past their expected return, and says so.
///
/// This is a duty-of-care signal before it is a compliance one. Someone who declared a
/// twenty-minute errand and has been gone three hours is the case this exists for, and nobody
/// finds that by waiting for them to record a return.
/// </summary>
public sealed class OverdueMovementNotifier(
    IMovementRepository movements,
    IEmployeeRepository employees,
    INotificationPublisher notifications) : IOverdueMovementNotifier
{
    /// <summary>
    /// Grace before an overdue movement is reported. Someone walking back through the door at
    /// the expected minute would otherwise be announced as missing.
    /// </summary>
    private static readonly TimeSpan Grace = TimeSpan.FromMinutes(15);

    /// <summary>
    /// How far back a sweep looks. An open movement older than this was almost certainly never
    /// closed rather than genuinely still running, and re-announcing it daily helps nobody.
    /// </summary>
    private const int LookbackDays = 2;

    public async Task<OverdueMovementSweepResult> SweepAsync(string tenantKey, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var open = await movements.QueryMovementsAsync(
            tenantKey,
            new MovementFilter(today.AddDays(-LookbackDays), today, null, null, "Open", OverdueOnly: true),
            ct);

        // The filter is trusted for the query and the record is trusted for the decision: the
        // record owns what overdue means, and the grace is this job's alone.
        var overdue = open.Where(movement => movement.IsOverdue(now - Grace)).ToList();

        var notified = 0;

        foreach (var movement in overdue)
        {
            var person = await employees.DescribeAsync(tenantKey, movement.EmployeeId, ct);
            var lateBy = (int)(now - movement.ExpectedReturnUtc).TotalMinutes;

            // Keyed to the movement, so the publisher reports it once per recipient however
            // often this runs. A person still out an hour later is not news twice.
            await notifications.PublishToPermissionAsync(
                new NotificationRequest(
                    tenantKey,
                    MovementNotifications.Category,
                    "Overdue return",
                    $"{person.DisplayName} went out at {movement.GoingOutUtc:HH\\:mm}, was expected " +
                    $"back by {movement.ExpectedReturnUtc:HH\\:mm}, and is {lateBy} minutes overdue. " +
                    $"{movement.Detail}",
                    "/movement/overdue",
                    "High",
                    MovementNotifications.Subject,
                    $"{movement.Id}:overdue"),
                "MOVEMENT.TEAM",
                ct);

            notified++;
        }

        return new OverdueMovementSweepResult(tenantKey, overdue.Count, notified);
    }
}
