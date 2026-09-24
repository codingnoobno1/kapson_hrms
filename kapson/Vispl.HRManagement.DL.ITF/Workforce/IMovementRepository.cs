using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.DL.ITF.Workforce;

public interface IMovementRepository
{
    // Movement
    Task<IReadOnlyList<MovementRecord>> QueryMovementsAsync(
        string tenantKey, MovementFilter filter, CancellationToken cancellationToken = default);

    Task<MovementRecord?> GetMovementAsync(
        string tenantKey, long movementId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The movement this employee has open, if any. Going out twice without returning would
    /// leave two records that both claim to be current.
    /// </summary>
    Task<MovementRecord?> GetOpenMovementAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default);

    Task<long> AddMovementAsync(MovementRecord movement, CancellationToken cancellationToken = default);

    Task UpdateMovementAsync(MovementRecord movement, CancellationToken cancellationToken = default);

    // Gate passes
    Task<IReadOnlyList<GatePassRecord>> QueryPassesAsync(
        string tenantKey, GatePassFilter filter, CancellationToken cancellationToken = default);

    Task<GatePassRecord?> GetPassAsync(
        string tenantKey, long passId, CancellationToken cancellationToken = default);

    /// <summary>Resolves a scanned token. Tenant-wide, because a gate scans before it knows who.</summary>
    Task<GatePassRecord?> FindPassByTokenAsync(
        string tenantKey, string token, CancellationToken cancellationToken = default);

    /// <summary>Approved or pending passes overlapping a range — the double-booking check.</summary>
    Task<IReadOnlyList<GatePassRecord>> GetOverlappingPassesAsync(
        string tenantKey, long employeeId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default);

    Task<long> AddPassAsync(GatePassRecord pass, CancellationToken cancellationToken = default);

    Task UpdatePassAsync(GatePassRecord pass, CancellationToken cancellationToken = default);

    // Scans
    Task<IReadOnlyList<GatePassScanRecord>> GetScansAsync(
        string tenantKey, long passId, CancellationToken cancellationToken = default);

    Task AddScanAsync(GatePassScanRecord scan, CancellationToken cancellationToken = default);
}
