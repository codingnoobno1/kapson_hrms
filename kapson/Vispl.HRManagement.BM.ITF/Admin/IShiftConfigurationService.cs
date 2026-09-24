using Vispl.HRManagement.Contracts.Admin;

namespace Vispl.HRManagement.BM.ITF.Admin;

/// <summary>
/// Administration of shifts: the definitions, and who works them.
///
/// These values decide when an arrival counts as late and how long a day is expected to be, so
/// everything the attendance engine measures traces back here.
/// </summary>
public interface IShiftConfigurationService
{
    Task<IReadOnlyList<ShiftDefinitionAdminDto>> GetDefinitionsAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    Task<long> SaveDefinitionAsync(
        ShiftDefinitionAdminDto definition, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftAssignmentAdminDto>> GetAssignmentsAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Everyone who can be given a shift, with the shift they work today. The picker needs the
    /// whole population, not only those already assigned — otherwise nobody could ever be the
    /// first person assigned.
    /// </summary>
    Task<IReadOnlyList<AssignableEmployeeDto>> GetAssignableEmployeesAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    Task<long> AssignAsync(
        AssignShiftRequest request, string? assignedBy, CancellationToken cancellationToken = default);

    Task RemoveAssignmentAsync(
        string tenantKey, long assignmentId, CancellationToken cancellationToken = default);
}
