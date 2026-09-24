using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.DL.Workforce;

public sealed class OnDutyRepository(HrManagementDbContext dbContext) : IOnDutyRepository
{
    public async Task<IReadOnlyList<OnDutyRequestRecord>> QueryAsync(
        string tenantKey, OnDutyFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.OnDutyRequests.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        // Any request overlapping the window, so one spanning the edge is not lost.
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

        var rows = await query
            .OrderByDescending(item => item.FromDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<OnDutyRequestRecord?> GetAsync(
        string tenantKey, long requestId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OnDutyRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == requestId, cancellationToken);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<OnDutyRequestRecord>> GetOverlappingAsync(
        string tenantKey, long employeeId, DateOnly from, DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.OnDutyRequests
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId &&
                           (item.State == "Pending" || item.State == "Approved") &&
                           item.FromDate <= to && item.ToDate >= from)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<long> AddAsync(
        OnDutyRequestRecord request, CancellationToken cancellationToken = default)
    {
        var entity = new OnDutyRequestEntity
        {
            TenantKey = request.TenantKey,
            EmployeeId = request.EmployeeId,
            Purpose = request.Purpose,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            Location = request.Location,
            Reason = request.Reason,
            State = request.State,
            RequestedUtc = request.RequestedUtc,
            UpdatedUtc = request.UpdatedUtc
        };

        dbContext.OnDutyRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateAsync(
        OnDutyRequestRecord request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.OnDutyRequests.FirstOrDefaultAsync(
            item => item.TenantKey == request.TenantKey && item.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.State = request.State;
        entity.DecidedByUserId = request.DecidedByUserId;
        entity.DecidedByName = request.DecidedByName;
        entity.DecidedUtc = request.DecidedUtc;
        entity.DecisionComment = request.DecisionComment;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static OnDutyRequestRecord Map(OnDutyRequestEntity entity) => new(
        entity.Id, entity.TenantKey, entity.EmployeeId, entity.Purpose, entity.FromDate, entity.ToDate,
        entity.Location, entity.Reason, entity.State, entity.DecidedByUserId, entity.DecidedByName,
        entity.DecidedUtc, entity.DecisionComment, entity.RequestedUtc, entity.UpdatedUtc);
}
