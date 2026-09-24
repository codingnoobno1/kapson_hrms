using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.DL.Workforce;

public sealed class MovementRepository(HrManagementDbContext dbContext) : IMovementRepository
{
    // ---- Movement ---------------------------------------------------------

    public async Task<IReadOnlyList<MovementRecord>> QueryMovementsAsync(
        string tenantKey, MovementFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.MovementRecords.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        if (filter.From is { } from)
        {
            query = query.Where(item => item.WorkDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(item => item.WorkDate <= to);
        }

        if (filter.EmployeeIds is { Count: > 0 } ids)
        {
            query = query.Where(item => ids.Contains(item.EmployeeId));
        }

        if (!string.IsNullOrWhiteSpace(filter.Kind))
        {
            query = query.Where(item => item.Kind == filter.Kind);
        }

        if (!string.IsNullOrWhiteSpace(filter.State))
        {
            query = query.Where(item => item.State == filter.State);
        }

        if (filter.OverdueOnly)
        {
            var now = DateTimeOffset.UtcNow;
            query = query.Where(item =>
                item.State == "Open" && item.ActualReturnUtc == null && item.ExpectedReturnUtc < now);
        }

        var rows = await query
            .OrderByDescending(item => item.GoingOutUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<MovementRecord?> GetMovementAsync(
        string tenantKey, long movementId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MovementRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == movementId, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<MovementRecord?> GetOpenMovementAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MovementRecords
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId &&
                           item.State == "Open" && item.ActualReturnUtc == null)
            .OrderByDescending(item => item.GoingOutUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<long> AddMovementAsync(
        MovementRecord movement, CancellationToken cancellationToken = default)
    {
        var entity = new MovementRecordEntity
        {
            TenantKey = movement.TenantKey,
            EmployeeId = movement.EmployeeId,
            Kind = movement.Kind,
            WorkDate = movement.WorkDate,
            GoingOutUtc = movement.GoingOutUtc,
            ExpectedReturnUtc = movement.ExpectedReturnUtc,
            ActualReturnUtc = movement.ActualReturnUtc,
            Detail = movement.Detail,
            DestinationLabel = movement.DestinationLabel,
            State = movement.State,
            RecordedBy = movement.RecordedBy,
            CreatedUtc = movement.CreatedUtc,
            UpdatedUtc = movement.UpdatedUtc
        };

        dbContext.MovementRecords.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateMovementAsync(
        MovementRecord movement, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MovementRecords.FirstOrDefaultAsync(
            item => item.TenantKey == movement.TenantKey && item.Id == movement.Id, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.ActualReturnUtc = movement.ActualReturnUtc;
        entity.State = movement.State;
        entity.Detail = movement.Detail;
        entity.DestinationLabel = movement.DestinationLabel;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Gate passes ------------------------------------------------------

    public async Task<IReadOnlyList<GatePassRecord>> QueryPassesAsync(
        string tenantKey, GatePassFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.GatePasses.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        // Any pass overlapping the window, so one spanning the edge is not lost.
        if (filter.From is { } from)
        {
            query = query.Where(item => item.ToDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(item => item.FromDate <= to);
        }

        if (filter.EmployeeIds is { Count: > 0 } ids)
        {
            query = query.Where(item => ids.Contains(item.EmployeeId));
        }

        if (!string.IsNullOrWhiteSpace(filter.State))
        {
            query = query.Where(item => item.State == filter.State);
        }

        if (!string.IsNullOrWhiteSpace(filter.PassType))
        {
            query = query.Where(item => item.PassType == filter.PassType);
        }

        var rows = await query
            .OrderByDescending(item => item.FromDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(MapPass).ToList();
    }

    public async Task<GatePassRecord?> GetPassAsync(
        string tenantKey, long passId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.GatePasses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == passId, cancellationToken);

        return entity is null ? null : MapPass(entity);
    }

    public async Task<GatePassRecord?> FindPassByTokenAsync(
        string tenantKey, string token, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.GatePasses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey && item.VerificationToken == token, cancellationToken);

        return entity is null ? null : MapPass(entity);
    }

    public async Task<IReadOnlyList<GatePassRecord>> GetOverlappingPassesAsync(
        string tenantKey, long employeeId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.GatePasses
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId &&
                           (item.State == "Pending" || item.State == "Approved") &&
                           item.FromDate <= to && item.ToDate >= from)
            .ToListAsync(cancellationToken);

        return rows.Select(MapPass).ToList();
    }

    public async Task<long> AddPassAsync(
        GatePassRecord pass, CancellationToken cancellationToken = default)
    {
        var entity = new GatePassEntity
        {
            TenantKey = pass.TenantKey,
            EmployeeId = pass.EmployeeId,
            PassType = pass.PassType,
            FromDate = pass.FromDate,
            ToDate = pass.ToDate,
            FromTime = pass.FromTime,
            ToTime = pass.ToTime,
            Purpose = pass.Purpose,
            DestinationLabel = pass.DestinationLabel,
            State = pass.State,
            RequestedUtc = pass.RequestedUtc,
            UpdatedUtc = pass.UpdatedUtc
        };

        dbContext.GatePasses.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdatePassAsync(
        GatePassRecord pass, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.GatePasses.FirstOrDefaultAsync(
            item => item.TenantKey == pass.TenantKey && item.Id == pass.Id, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.State = pass.State;
        entity.VerificationToken = pass.VerificationToken;
        entity.DecidedByUserId = pass.DecidedByUserId;
        entity.DecidedByName = pass.DecidedByName;
        entity.DecidedUtc = pass.DecidedUtc;
        entity.DecisionComment = pass.DecisionComment;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Scans ------------------------------------------------------------

    public async Task<IReadOnlyList<GatePassScanRecord>> GetScansAsync(
        string tenantKey, long passId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.GatePassScans
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.GatePassId == passId)
            .OrderBy(item => item.ScannedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(MapScan).ToList();
    }

    public async Task AddScanAsync(
        GatePassScanRecord scan, CancellationToken cancellationToken = default)
    {
        dbContext.GatePassScans.Add(new GatePassScanEntity
        {
            TenantKey = scan.TenantKey,
            GatePassId = scan.GatePassId,
            Direction = scan.Direction,
            ScannedUtc = scan.ScannedUtc,
            ScannedBy = scan.ScannedBy,
            GateLabel = scan.GateLabel
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Mapping ----------------------------------------------------------

    private static MovementRecord Map(MovementRecordEntity entity) => new(
        entity.Id, entity.TenantKey, entity.EmployeeId, entity.Kind, entity.WorkDate,
        entity.GoingOutUtc, entity.ExpectedReturnUtc, entity.ActualReturnUtc, entity.Detail,
        entity.DestinationLabel, entity.State, entity.RecordedBy, entity.CreatedUtc, entity.UpdatedUtc);

    private static GatePassRecord MapPass(GatePassEntity entity) => new(
        entity.Id, entity.TenantKey, entity.EmployeeId, entity.PassType, entity.FromDate,
        entity.ToDate, entity.FromTime, entity.ToTime, entity.Purpose, entity.DestinationLabel,
        entity.State, entity.VerificationToken, entity.DecidedByUserId, entity.DecidedByName,
        entity.DecidedUtc, entity.DecisionComment, entity.RequestedUtc, entity.UpdatedUtc);

    private static GatePassScanRecord MapScan(GatePassScanEntity entity) => new(
        entity.Id, entity.TenantKey, entity.GatePassId, entity.Direction, entity.ScannedUtc,
        entity.ScannedBy, entity.GateLabel);
}
