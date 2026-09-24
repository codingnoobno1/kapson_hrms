using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.DL.ITF.Workforce;

public interface IShiftRepository
{
    Task<IReadOnlyList<ShiftDefinitionRecord>> GetDefinitionsAsync(
        string tenantKey, bool activeOnly = true, CancellationToken cancellationToken = default);

    Task<ShiftDefinitionRecord?> GetDefinitionAsync(
        string tenantKey, long definitionId, CancellationToken cancellationToken = default);

    Task<ShiftDefinitionRecord?> GetDefaultDefinitionAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a definition. Marking one default clears the flag on every other, because "the
    /// shift used when nobody assigned one" cannot sensibly be two shifts.
    /// </summary>
    Task<long> UpsertDefinitionAsync(
        ShiftDefinitionRecord definition, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShiftAssignmentRecord>> GetAssignmentsAsync(
        string tenantKey, IReadOnlyList<long> employeeIds, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    Task<long> AddAssignmentAsync(
        ShiftAssignmentRecord assignment, CancellationToken cancellationToken = default);

    Task RemoveAssignmentAsync(
        string tenantKey, long assignmentId, CancellationToken cancellationToken = default);
}
