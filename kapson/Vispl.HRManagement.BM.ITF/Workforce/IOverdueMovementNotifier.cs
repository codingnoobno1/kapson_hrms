namespace Vispl.HRManagement.BM.ITF.Workforce;

/// <summary>Names the notification vocabulary movement shares between the service and the sweep.</summary>
public static class MovementNotifications
{
    public const string Category = "Movement";

    /// <summary>Subject type, so a repeat about the same movement is not stacked in an inbox.</summary>
    public const string Subject = "Movement";
}

/// <summary>What one sweep found, for the log.</summary>
public sealed record OverdueMovementSweepResult(
    string TenantKey,

    /// <summary>Movements still open with their expected return already passed.</summary>
    int OverdueFound,

    /// <summary>Movements this pass raised a notification for. The rest were already reported.</summary>
    int Notified);

/// <summary>
/// Tells someone when a person who went out has not come back.
///
/// Every other movement and gate pass notification hangs off a gesture — a request, a decision,
/// a return. Overdue has none: the whole point is that the employee did not do the thing that
/// would have raised it. So it can only be found by looking, which is why this is a swept job
/// rather than a call inside <c>MovementService</c>.
///
/// Duplicate suppression is the publisher's, not this class's. Each notification is keyed to
/// the movement it concerns, so a sweep every few minutes reports a given movement once per
/// recipient and then goes quiet about it.
/// </summary>
public interface IOverdueMovementNotifier
{
    Task<OverdueMovementSweepResult> SweepAsync(string tenantKey, CancellationToken ct = default);
}
