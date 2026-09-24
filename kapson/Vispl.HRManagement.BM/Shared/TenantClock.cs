using Vispl.HRManagement.BM.ITF.Shared;
using Vispl.HRManagement.DL.ITF.Admin;

namespace Vispl.HRManagement.BM.Shared;

/// <summary>
/// Resolves what "today" means for a client.
///
/// Calendar-day decisions cannot be made in UTC. A gate pass valid on the 4th is valid on the
/// 4th where the gate stands: in India that is five and a half hours ahead of UTC, so a pass
/// checked against a UTC date would be refused for the first five and a half hours of every day
/// it is valid.
/// </summary>
public sealed class TenantClock(ITenantRepository tenants) : ITenantClock
{
    public async Task<DateOnly> TodayAsync(string tenantKey, CancellationToken ct = default)
    {
        var zone = await ResolveZoneAsync(tenantKey, ct);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
    }

    public async Task<TimeZoneInfo> ResolveZoneAsync(string tenantKey, CancellationToken ct = default)
    {
        var tenant = await tenants.GetByKeyAsync(tenantKey, ct);
        var id = tenant?.TimeZoneId;

        if (string.IsNullOrWhiteSpace(id))
        {
            return TimeZoneInfo.Utc;
        }

        // A zone id that this machine does not recognise falls back to UTC rather than throwing.
        // A misconfigured client should see slightly wrong dates, not a dead gate.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (Exception exception) when (
            exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
