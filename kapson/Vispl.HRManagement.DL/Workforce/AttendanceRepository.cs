using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.DL.Workforce;

public sealed class AttendanceRepository(HrManagementDbContext dbContext) : IAttendanceRepository
{
    // ---- Punches ----------------------------------------------------------

    public async Task<IReadOnlyList<AttendancePunchRecord>> GetPunchesAsync(
        string tenantKey, long employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.AttendancePunches
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           item.EmployeeId == employeeId &&
                           item.WorkDate >= from &&
                           item.WorkDate <= to &&
                           item.SupersededByCorrectionId == null)
            .OrderBy(item => item.OccurredUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(MapPunch).ToList();
    }

    public async Task<IReadOnlyList<AttendancePunchRecord>> GetPunchesForAsync(
        string tenantKey, IReadOnlyList<long> employeeIds, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.AttendancePunches
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           item.WorkDate >= from &&
                           item.WorkDate <= to &&
                           item.SupersededByCorrectionId == null &&
                           employeeIds.Contains(item.EmployeeId))
            .OrderBy(item => item.OccurredUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(MapPunch).ToList();
    }

    public async Task<AttendancePunchRecord?> GetLastPunchAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttendancePunches
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           item.EmployeeId == employeeId &&
                           item.SupersededByCorrectionId == null)
            .OrderByDescending(item => item.OccurredUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : MapPunch(entity);
    }

    public async Task<AttendancePunchRecord?> FindByClientEventAsync(
        string tenantKey, long employeeId, string clientEventId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttendancePunches
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey &&
                        item.EmployeeId == employeeId &&
                        item.ClientEventId == clientEventId,
                cancellationToken);

        return entity is null ? null : MapPunch(entity);
    }

    public async Task<long> AddPunchAsync(
        AttendancePunchRecord punch, CancellationToken cancellationToken = default)
    {
        var entity = new AttendancePunchEntity
        {
            TenantKey = punch.TenantKey,
            EmployeeId = punch.EmployeeId,
            WorkDate = punch.WorkDate,
            Type = punch.Type,
            OccurredUtc = punch.OccurredUtc,
            Source = punch.Source,
            DeviceId = punch.DeviceId,
            LocationLabel = punch.LocationLabel,
            Latitude = punch.Latitude,
            Longitude = punch.Longitude,
            ClientEventId = punch.ClientEventId,
            CreatedUtc = punch.CreatedUtc
        };

        dbContext.AttendancePunches.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task SupersedePunchesAsync(
        string tenantKey, long employeeId, DateOnly workDate, long correctionId,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.AttendancePunches
            .Where(item => item.TenantKey == tenantKey &&
                           item.EmployeeId == employeeId &&
                           item.WorkDate == workDate &&
                           item.SupersededByCorrectionId == null)
            .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            row.SupersededByCorrectionId = correctionId;
        }

        if (rows.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    // ---- Days -------------------------------------------------------------

    public async Task<IReadOnlyList<AttendanceDayRecord>> QueryDaysAsync(
        string tenantKey, AttendanceDayFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AttendanceDays.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        if (filter.EmployeeId is { } employeeId)
        {
            query = query.Where(item => item.EmployeeId == employeeId);
        }

        if (filter.EmployeeIds is { Count: > 0 } ids)
        {
            query = query.Where(item => ids.Contains(item.EmployeeId));
        }

        if (filter.From is { } from)
        {
            query = query.Where(item => item.WorkDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(item => item.WorkDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(item => item.Status == filter.Status);
        }

        if (filter.ExceptionsOnly)
        {
            query = query.Where(item => item.ExceptionsJson != "[]");
        }

        var rows = await query
            .OrderByDescending(item => item.WorkDate)
            .ThenBy(item => item.EmployeeId)
            .ToListAsync(cancellationToken);

        return rows.Select(MapDay).ToList();
    }

    public async Task<AttendanceDayRecord?> GetDayAsync(
        string tenantKey, long employeeId, DateOnly workDate, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttendanceDays
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey &&
                        item.EmployeeId == employeeId &&
                        item.WorkDate == workDate,
                cancellationToken);

        return entity is null ? null : MapDay(entity);
    }

    public async Task<long> UpsertDayAsync(
        AttendanceDayRecord day, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttendanceDays.FirstOrDefaultAsync(
            item => item.TenantKey == day.TenantKey &&
                    item.EmployeeId == day.EmployeeId &&
                    item.WorkDate == day.WorkDate,
            cancellationToken);

        if (entity is null)
        {
            entity = new AttendanceDayEntity
            {
                TenantKey = day.TenantKey,
                EmployeeId = day.EmployeeId,
                WorkDate = day.WorkDate
            };
            dbContext.AttendanceDays.Add(entity);
        }

        entity.ShiftCode = day.ShiftCode;
        entity.Status = day.Status;
        entity.WorkedMinutes = day.WorkedMinutes;
        entity.ExpectedMinutes = day.ExpectedMinutes;
        entity.ExceptionsJson = day.ExceptionsJson;
        entity.UpdatedUtc = day.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStatusAsync(
        string tenantKey, DateOnly workDate, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.AttendanceDays
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.WorkDate == workDate)
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Status, row => row.Count, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStatusForAsync(
        string tenantKey, DateOnly workDate, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return new Dictionary<string, int>();
        }

        var rows = await dbContext.AttendanceDays
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.WorkDate == workDate &&
                           employeeIds.Contains(item.EmployeeId))
            .GroupBy(item => item.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(item => item.Status, item => item.Count);
    }

    public async Task<IReadOnlyDictionary<long, DateTimeOffset>> GetFirstInPunchAsync(
        string tenantKey, DateOnly workDate, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return new Dictionary<long, DateTimeOffset>();
        }

        // Superseded punches are excluded: a corrected day must be measured against the
        // corrected arrival, not the one the correction replaced.
        var rows = await dbContext.AttendancePunches
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.WorkDate == workDate &&
                           item.Type == "In" && item.SupersededByCorrectionId == null &&
                           employeeIds.Contains(item.EmployeeId))
            .GroupBy(item => item.EmployeeId)
            .Select(group => new { EmployeeId = group.Key, First = group.Min(item => item.OccurredUtc) })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(item => item.EmployeeId, item => item.First);
    }

    public async Task<IReadOnlyList<(long EmployeeId, DateOnly WorkDate, DateTimeOffset FirstIn)>>
        GetFirstInPunchRangeAsync(
            string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
            CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var rangeStart = from;
        var rangeEnd = to;

        var rows = await dbContext.AttendancePunches
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           item.WorkDate >= rangeStart && item.WorkDate <= rangeEnd &&
                           item.Type == "In" && item.SupersededByCorrectionId == null &&
                           employeeIds.Contains(item.EmployeeId))
            .GroupBy(item => new { item.EmployeeId, item.WorkDate })
            .Select(group => new
            {
                group.Key.EmployeeId,
                group.Key.WorkDate,
                FirstIn = group.Min(item => item.OccurredUtc)
            })
            .ToListAsync(cancellationToken);

        return rows.Select(item => (item.EmployeeId, item.WorkDate, item.FirstIn)).ToList();
    }

    public async Task<IReadOnlyList<(long EmployeeId, DateOnly WorkDate, int InCount, int OutCount)>>
        GetPunchCountsAsync(
            string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
            CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var rangeStart = from;
        var rangeEnd = to;

        var rows = await dbContext.AttendancePunches
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           item.WorkDate >= rangeStart && item.WorkDate <= rangeEnd &&
                           item.SupersededByCorrectionId == null &&
                           employeeIds.Contains(item.EmployeeId))
            .GroupBy(item => new { item.EmployeeId, item.WorkDate })
            .Select(group => new
            {
                group.Key.EmployeeId,
                group.Key.WorkDate,
                InCount = group.Count(item => item.Type == "In"),
                OutCount = group.Count(item => item.Type == "Out")
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(item => (item.EmployeeId, item.WorkDate, item.InCount, item.OutCount))
            .ToList();
    }

    public async Task<IReadOnlyList<AttendanceExceptionRecord>> QueryExceptionsForAsync(
        string tenantKey, IReadOnlyList<long> employeeIds, DateOnly from, DateOnly to,
        string? exceptionType, string? state, CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var query = dbContext.AttendanceExceptions
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           item.WorkDate >= from && item.WorkDate <= to &&
                           employeeIds.Contains(item.EmployeeId));

        if (!string.IsNullOrWhiteSpace(exceptionType))
        {
            query = query.Where(item => item.ExceptionType == exceptionType);
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            query = query.Where(item => item.State == state);
        }

        var rows = await query
            .OrderByDescending(item => item.WorkDate)
            .ThenBy(item => item.EmployeeId)
            .ToListAsync(cancellationToken);

        return rows.Select(MapException).ToList();
    }

    // ---- Corrections ------------------------------------------------------

    public async Task<IReadOnlyList<AttendanceCorrectionRecord>> QueryCorrectionsAsync(
        string tenantKey, long? employeeId, string? state, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AttendanceCorrections.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        if (employeeId is { } id)
        {
            query = query.Where(item => item.EmployeeId == id);
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            query = query.Where(item => item.State == state);
        }

        var rows = await query
            .OrderByDescending(item => item.SubmittedUtc)
            .ToListAsync(cancellationToken);

        return rows.Select(MapCorrection).ToList();
    }

    public async Task<AttendanceCorrectionRecord?> GetCorrectionAsync(
        string tenantKey, long correctionId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttendanceCorrections
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == correctionId, cancellationToken);

        return entity is null ? null : MapCorrection(entity);
    }

    public async Task<AttendanceCorrectionRecord?> GetPendingCorrectionAsync(
        string tenantKey, long employeeId, DateOnly workDate, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttendanceCorrections
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey &&
                        item.EmployeeId == employeeId &&
                        item.WorkDate == workDate &&
                        item.State == "Pending",
                cancellationToken);

        return entity is null ? null : MapCorrection(entity);
    }

    public async Task<long> UpsertCorrectionAsync(
        AttendanceCorrectionRecord correction, CancellationToken cancellationToken = default)
    {
        var entity = correction.Id > 0
            ? await dbContext.AttendanceCorrections.FirstOrDefaultAsync(
                item => item.TenantKey == correction.TenantKey && item.Id == correction.Id, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new AttendanceCorrectionEntity
            {
                TenantKey = correction.TenantKey,
                EmployeeId = correction.EmployeeId,
                WorkDate = correction.WorkDate,
                SubmittedUtc = correction.SubmittedUtc
            };
            dbContext.AttendanceCorrections.Add(entity);
        }

        entity.ReasonCode = correction.ReasonCode;
        entity.Comment = correction.Comment;
        entity.OriginalPunchesJson = correction.OriginalPunchesJson;
        entity.RequestedPunchesJson = correction.RequestedPunchesJson;
        entity.State = correction.State;
        entity.DecisionReasonCode = correction.DecisionReasonCode;
        entity.DecisionComment = correction.DecisionComment;
        entity.DecidedBy = correction.DecidedBy;
        entity.DecidedUtc = correction.DecidedUtc;
        entity.UpdatedUtc = correction.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    // ---- Exceptions -------------------------------------------------------

    public async Task<IReadOnlyList<AttendanceExceptionRecord>> QueryExceptionsAsync(
        string tenantKey, long? employeeId, DateOnly? from, DateOnly? to, string? exceptionType, string? state,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AttendanceExceptions.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        if (employeeId is { } id)
        {
            query = query.Where(item => item.EmployeeId == id);
        }

        if (from is { } fromDate)
        {
            query = query.Where(item => item.WorkDate >= fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(item => item.WorkDate <= toDate);
        }

        if (!string.IsNullOrWhiteSpace(exceptionType))
        {
            query = query.Where(item => item.ExceptionType == exceptionType);
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            query = query.Where(item => item.State == state);
        }

        var rows = await query
            .OrderByDescending(item => item.WorkDate)
            .ToListAsync(cancellationToken);

        return rows.Select(MapException).ToList();
    }

    public async Task<AttendanceExceptionRecord?> GetExceptionAsync(
        string tenantKey, long exceptionId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttendanceExceptions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == exceptionId, cancellationToken);

        return entity is null ? null : MapException(entity);
    }

    public async Task UpsertExceptionAsync(
        AttendanceExceptionRecord exception, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AttendanceExceptions.FirstOrDefaultAsync(
            item => item.TenantKey == exception.TenantKey &&
                    item.EmployeeId == exception.EmployeeId &&
                    item.WorkDate == exception.WorkDate &&
                    item.ExceptionType == exception.ExceptionType,
            cancellationToken);

        if (entity is null)
        {
            entity = new AttendanceExceptionEntity
            {
                TenantKey = exception.TenantKey,
                EmployeeId = exception.EmployeeId,
                WorkDate = exception.WorkDate,
                ExceptionType = exception.ExceptionType,
                DetectedUtc = exception.DetectedUtc
            };
            dbContext.AttendanceExceptions.Add(entity);
        }

        entity.DurationMinutes = exception.DurationMinutes;
        entity.PolicyCode = exception.PolicyCode;

        // A recomputation refreshes the measurement but must not undo a human decision.
        if (string.Equals(entity.State, "Open", StringComparison.OrdinalIgnoreCase))
        {
            entity.State = exception.State;
        }

        entity.EmployeeJustification = exception.EmployeeJustification ?? entity.EmployeeJustification;
        entity.ApprovedMinutes = exception.ApprovedMinutes ?? entity.ApprovedMinutes;
        entity.DecisionReasonCode = exception.DecisionReasonCode ?? entity.DecisionReasonCode;
        entity.DecisionComment = exception.DecisionComment ?? entity.DecisionComment;
        entity.DecidedBy = exception.DecidedBy ?? entity.DecidedBy;
        entity.DecidedUtc = exception.DecidedUtc ?? entity.DecidedUtc;
        entity.UpdatedUtc = exception.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveExceptionsAsync(
        string tenantKey, long employeeId, DateOnly workDate, IReadOnlyList<string> keepTypes,
        CancellationToken cancellationToken = default)
    {
        // Only untouched exceptions are cleared. One a person has confirmed or decided stays,
        // because deleting it would erase their decision.
        var rows = await dbContext.AttendanceExceptions
            .Where(item => item.TenantKey == tenantKey &&
                           item.EmployeeId == employeeId &&
                           item.WorkDate == workDate &&
                           item.State == "Open" &&
                           !keepTypes.Contains(item.ExceptionType))
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return;
        }

        dbContext.AttendanceExceptions.RemoveRange(rows);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Mapping ----------------------------------------------------------

    private static AttendancePunchRecord MapPunch(AttendancePunchEntity item) => new(
        item.Id, item.TenantKey, item.EmployeeId, item.WorkDate, item.Type, item.OccurredUtc,
        item.Source, item.DeviceId, item.LocationLabel, item.Latitude, item.Longitude,
        item.ClientEventId, item.SupersededByCorrectionId, item.CreatedUtc);

    private static AttendanceDayRecord MapDay(AttendanceDayEntity item) => new(
        item.Id, item.TenantKey, item.EmployeeId, item.WorkDate, item.ShiftCode, item.Status,
        item.WorkedMinutes, item.ExpectedMinutes, item.ExceptionsJson, item.UpdatedUtc);

    private static AttendanceCorrectionRecord MapCorrection(AttendanceCorrectionEntity item) => new(
        item.Id, item.TenantKey, item.EmployeeId, item.WorkDate, item.ReasonCode, item.Comment,
        item.OriginalPunchesJson, item.RequestedPunchesJson, item.State, item.DecisionReasonCode,
        item.DecisionComment, item.DecidedBy, item.DecidedUtc, item.SubmittedUtc, item.UpdatedUtc);

    private static AttendanceExceptionRecord MapException(AttendanceExceptionEntity item) => new(
        item.Id, item.TenantKey, item.EmployeeId, item.WorkDate, item.ExceptionType, item.DurationMinutes,
        item.State, item.PolicyCode, item.EmployeeJustification, item.ApprovedMinutes,
        item.DecisionReasonCode, item.DecisionComment, item.DecidedBy, item.DecidedUtc,
        item.DetectedUtc, item.UpdatedUtc);
}
