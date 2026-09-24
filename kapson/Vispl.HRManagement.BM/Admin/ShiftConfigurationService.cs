using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Employee;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Admin;

/// <summary>
/// Editing of shifts and who works them.
///
/// A shift decides when someone is late and how long their day is expected to be, so the guards
/// here refuse configurations that would make attendance meaningless rather than merely odd.
/// </summary>
public sealed class ShiftConfigurationService(
    IShiftRepository shifts,
    IEmployeeRepository employees) : IShiftConfigurationService
{
    public async Task<IReadOnlyList<ShiftDefinitionAdminDto>> GetDefinitionsAsync(
        string tenantKey, CancellationToken cancellationToken = default)
    {
        var key = Normalize(tenantKey);

        // Inactive shifts are listed: the panel is where one gets switched back on.
        var definitions = await shifts.GetDefinitionsAsync(key, activeOnly: false, cancellationToken);
        var employeeIds = await employees.GetIdsAsync(
            key, new EmployeeQueryFilter(null, null, null, null, null, ActiveOnly: true), cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignments = await shifts.GetAssignmentsAsync(key, employeeIds, today, today, cancellationToken);

        var counts = assignments
            .GroupBy(item => item.ShiftDefinitionId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.EmployeeId).Distinct().Count());

        return definitions.Select(definition =>
        {
            var dto = ToDto(definition);
            dto.AssignedCount = counts.GetValueOrDefault(definition.Id);
            return dto;
        }).ToList();
    }

    public async Task<long> SaveDefinitionAsync(
        ShiftDefinitionAdminDto definition, CancellationToken cancellationToken = default)
    {
        var key = Normalize(definition.TenantKey);
        var code = definition.Code.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("A shift needs a code.");
        }

        var mask = definition.ToWorkingDaysMask();

        if (!mask.Contains('Y'))
        {
            throw new InvalidOperationException(
                "A shift has to run on at least one day of the week.");
        }

        // A shift that ends before it starts is either a night shift or a typo, and the two
        // cannot be told apart without asking.
        if (!definition.CrossesMidnight && definition.EndTime <= definition.StartTime)
        {
            throw new InvalidOperationException(
                "The end time is not after the start time. Tick \"crosses midnight\" for a night shift.");
        }

        if (definition.ExpectedMinutes <= 0)
        {
            throw new InvalidOperationException(
                "The break is as long as the shift, so this shift expects no work at all.");
        }

        if (definition.ExpectedMinutes > 16 * 60)
        {
            throw new InvalidOperationException(
                "A shift longer than sixteen paid hours is almost certainly a mistake.");
        }

        var existing = await shifts.GetDefinitionsAsync(key, activeOnly: false, cancellationToken);
        var current = existing.FirstOrDefault(item => item.Code == code);

        // A tenant must keep exactly one default. Clearing the last one would leave anyone
        // without an assignment with no expected time at all.
        if (current is { IsDefault: true } && !definition.IsDefault)
        {
            throw new InvalidOperationException(
                "This is the default shift. Make another shift the default instead of clearing this one.");
        }

        if (current is { IsActive: true } && !definition.IsActive && current.IsDefault)
        {
            throw new InvalidOperationException("The default shift cannot be deactivated.");
        }

        var record = new ShiftDefinitionRecord(
            current?.Id ?? 0,
            key,
            code,
            definition.Name.Trim(),
            definition.StartTime,
            definition.EndTime,
            definition.BreakMinutes,
            definition.GraceMinutes,
            mask,
            definition.CrossesMidnight,
            definition.IsDefault,
            definition.IsActive);

        return await shifts.UpsertDefinitionAsync(record, cancellationToken);
    }

    public async Task<IReadOnlyList<ShiftAssignmentAdminDto>> GetAssignmentsAsync(
        string tenantKey, CancellationToken cancellationToken = default)
    {
        var key = Normalize(tenantKey);
        var filter = new EmployeeQueryFilter(null, null, null, null, null, ActiveOnly: true);

        var staff = (await employees.QueryAsync(key, filter, cancellationToken))
            .ToDictionary(person => person.Id);

        var definitions = (await shifts.GetDefinitionsAsync(key, activeOnly: false, cancellationToken))
            .ToDictionary(definition => definition.Id);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var assignments = await shifts.GetAssignmentsAsync(
            key, staff.Keys.ToList(), today.AddYears(-1), today.AddYears(1), cancellationToken);

        return assignments
            .Where(item => staff.ContainsKey(item.EmployeeId) &&
                           definitions.ContainsKey(item.ShiftDefinitionId))
            .Select(item =>
            {
                var person = staff[item.EmployeeId];
                var definition = definitions[item.ShiftDefinitionId];

                return new ShiftAssignmentAdminDto(
                    item.Id,
                    item.EmployeeId,
                    $"{person.LegalFirstName} {person.LegalLastName}".Trim(),
                    person.DepartmentCode,
                    definition.Code,
                    definition.Name,
                    item.EffectiveFrom,
                    item.EffectiveTo,
                    item.Note);
            })
            .OrderBy(item => item.EmployeeName)
            .ThenByDescending(item => item.EffectiveFrom)
            .ToList();
    }

    public async Task<IReadOnlyList<AssignableEmployeeDto>> GetAssignableEmployeesAsync(
        string tenantKey, CancellationToken cancellationToken = default)
    {
        var key = Normalize(tenantKey);
        var filter = new EmployeeQueryFilter(null, null, null, null, null, ActiveOnly: true);
        var staff = await employees.QueryAsync(key, filter, cancellationToken);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var definitions = (await shifts.GetDefinitionsAsync(key, activeOnly: false, cancellationToken))
            .ToDictionary(definition => definition.Id);

        var assignments = await shifts.GetAssignmentsAsync(
            key, staff.Select(person => person.Id).ToList(), today, today, cancellationToken);

        // The assignment in force today: latest start wins, most recently entered breaks a tie.
        var current = assignments
            .GroupBy(item => item.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(item => item.EffectiveFrom)
                    .ThenByDescending(item => item.AssignedUtc)
                    .First()
                    .ShiftDefinitionId);

        return staff
            .Select(person => new AssignableEmployeeDto(
                person.Id,
                $"{person.LegalFirstName} {person.LegalLastName}".Trim(),
                person.DepartmentCode,
                current.TryGetValue(person.Id, out var shiftId) && definitions.TryGetValue(shiftId, out var definition)
                    ? definition.Code
                    : null))
            .OrderBy(person => person.Name)
            .ToList();
    }

    public async Task<long> AssignAsync(
        AssignShiftRequest request, string? assignedBy, CancellationToken cancellationToken = default)
    {
        var key = Normalize(request.TenantKey);

        if (request.EffectiveTo is { } to && to < request.EffectiveFrom)
        {
            throw new InvalidOperationException("The assignment ends before it starts.");
        }

        var definition = await shifts.GetDefinitionAsync(key, request.ShiftDefinitionId, cancellationToken)
                         ?? throw new InvalidOperationException("That shift no longer exists.");

        if (!definition.IsActive)
        {
            throw new InvalidOperationException($"{definition.Name} is not active.");
        }

        var employee = await employees.GetAsync(key, request.EmployeeId, cancellationToken)
                       ?? throw new InvalidOperationException("That employee no longer exists.");

        return await shifts.AddAssignmentAsync(
            new ShiftAssignmentRecord(
                0, key, employee.Id, definition.Id, request.EffectiveFrom, request.EffectiveTo,
                DateTimeOffset.UtcNow, assignedBy, request.Note?.Trim()),
            cancellationToken);
    }

    public Task RemoveAssignmentAsync(
        string tenantKey, long assignmentId, CancellationToken cancellationToken = default) =>
        shifts.RemoveAssignmentAsync(Normalize(tenantKey), assignmentId, cancellationToken);

    private static ShiftDefinitionAdminDto ToDto(ShiftDefinitionRecord definition)
    {
        var dto = new ShiftDefinitionAdminDto
        {
            Id = definition.Id,
            TenantKey = definition.TenantKey,
            Code = definition.Code,
            Name = definition.Name,
            StartTime = definition.StartTime,
            EndTime = definition.EndTime,
            BreakMinutes = definition.BreakMinutes,
            GraceMinutes = definition.GraceMinutes,
            CrossesMidnight = definition.CrossesMidnight,
            IsDefault = definition.IsDefault,
            IsActive = definition.IsActive
        };

        dto.ApplyWorkingDaysMask(definition.WorkingDays);
        return dto;
    }

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToLowerInvariant();
    }
}
