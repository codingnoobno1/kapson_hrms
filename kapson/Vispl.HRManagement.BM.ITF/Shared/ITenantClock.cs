namespace Vispl.HRManagement.BM.ITF.Shared;

/// <summary>
/// What "today" is for a client, in the client's own zone.
///
/// Any decision about a calendar date rather than an instant goes through here.
/// </summary>
public interface ITenantClock
{
    Task<DateOnly> TodayAsync(string tenantKey, CancellationToken ct = default);

    Task<TimeZoneInfo> ResolveZoneAsync(string tenantKey, CancellationToken ct = default);
}
