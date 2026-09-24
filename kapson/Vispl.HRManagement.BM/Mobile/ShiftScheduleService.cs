using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Mobile;

/// <summary>
/// Resolves expected shifts for a population and range.
///
/// Assignments are dated ranges, so the shift for a given day is the assignment covering it —
/// and where two overlap, the one that starts later wins, with the more recently entered one
/// breaking a tie. A roster correction entered today therefore supersedes one entered last
/// month for the same period, which is what a correction is for.
/// </summary>
public sealed class ShiftScheduleService(IShiftRepository shifts) : IShiftScheduleService
{
    /// <summary>
    /// Used when a tenant has configured no shifts at all. Matches what the platform assumed
    /// before shifts existed, so behaviour does not change until a tenant configures its own.
    /// </summary>
    private static readonly ShiftDefinitionRecord ImplicitDefault = new(
        0, string.Empty, "GENERAL", "General shift",
        new TimeOnly(9, 0), new TimeOnly(18, 0), 60, 15, "NYYYYYN", false, true, true);

    public async Task<ShiftSchedule> ResolveAsync(
        string tenantKey,
        IReadOnlyList<long> employeeIds,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        var entries = new Dictionary<(long, DateOnly), ExpectedShiftRecord>();

        if (employeeIds.Count == 0 || to < from)
        {
            return new ShiftSchedule(entries, usesFallbackOnly: true, fallback: null);
        }

        var definitions = (await shifts.GetDefinitionsAsync(tenantKey, true, ct))
            .ToDictionary(definition => definition.Id);

        var fallback = await shifts.GetDefaultDefinitionAsync(tenantKey, ct) ?? ImplicitDefault;
        var assignments = await shifts.GetAssignmentsAsync(tenantKey, employeeIds, from, to, ct);

        var byEmployee = assignments
            .GroupBy(assignment => assignment.EmployeeId)
            .ToDictionary(group => group.Key, group => group.ToList());

        var anyRealAssignment = false;

        foreach (var employeeId in employeeIds)
        {
            var employeeAssignments = byEmployee.GetValueOrDefault(employeeId) ?? [];

            for (var date = from; date <= to; date = date.AddDays(1))
            {
                var assignment = employeeAssignments
                    .Where(item => item.EffectiveFrom <= date &&
                                   (item.EffectiveTo is null || item.EffectiveTo >= date))
                    .OrderByDescending(item => item.EffectiveFrom)
                    .ThenByDescending(item => item.AssignedUtc)
                    .FirstOrDefault();

                var definition = assignment is not null &&
                                 definitions.TryGetValue(assignment.ShiftDefinitionId, out var assigned)
                    ? assigned
                    : null;

                var isFallback = definition is null;
                definition ??= fallback;

                if (!isFallback)
                {
                    anyRealAssignment = true;
                }

                entries[(employeeId, date)] = new ExpectedShiftRecord(
                    employeeId,
                    date,
                    definition.Code,
                    definition.Name,
                    definition.StartTime,
                    definition.EndTime,
                    definition.ExpectedMinutes,
                    definition.GraceMinutes,
                    definition.WorksOn(date.DayOfWeek),
                    isFallback);
            }
        }

        return new ShiftSchedule(entries, !anyRealAssignment, fallback);
    }
}
