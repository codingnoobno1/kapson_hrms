using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.DL.Workforce;

public sealed class LeaveRepository(HrManagementDbContext dbContext) : ILeaveRepository
{
    /// <summary>States that still hold a claim on a date: approved, or waiting for a decision.</summary>
    private static readonly string[] LiveStates = ["Pending", "Approved"];

    // ---- Policies ---------------------------------------------------------

    public async Task<IReadOnlyList<LeavePolicyRecord>> GetPoliciesAsync(
        string tenantKey, bool activeOnly = true, CancellationToken cancellationToken = default)
    {
        var query = dbContext.LeavePolicies.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        if (activeOnly)
        {
            query = query.Where(item => item.IsActive);
        }

        var rows = await query.OrderBy(item => item.Code).ToListAsync(cancellationToken);
        return rows.Select(MapPolicy).ToList();
    }

    public async Task<LeavePolicyRecord?> GetPolicyAsync(
        string tenantKey, long policyId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LeavePolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == policyId, cancellationToken);
        return entity is null ? null : MapPolicy(entity);
    }

    public async Task<LeavePolicyRecord?> GetPolicyByCodeAsync(
        string tenantKey, string code, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LeavePolicies
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Code == code, cancellationToken);
        return entity is null ? null : MapPolicy(entity);
    }

    public async Task<long> UpsertPolicyAsync(
        LeavePolicyRecord policy, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LeavePolicies.FirstOrDefaultAsync(
            item => item.TenantKey == policy.TenantKey && item.Code == policy.Code, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        if (entity is null)
        {
            entity = new LeavePolicyEntity
            {
                TenantKey = policy.TenantKey,
                Code = policy.Code,
                CreatedUtc = now
            };
            dbContext.LeavePolicies.Add(entity);
        }

        entity.Name = policy.Name;
        entity.Unit = policy.Unit;
        entity.Version = policy.Version;
        entity.AnnualEntitlement = policy.AnnualEntitlement;
        entity.AccrualMode = policy.AccrualMode;
        entity.CarryForwardCap = policy.CarryForwardCap;
        entity.NoticeDays = policy.NoticeDays;
        entity.DocumentRequiredAboveDays = policy.DocumentRequiredAboveDays;
        entity.AllowsHalfDay = policy.AllowsHalfDay;
        entity.AllowsNegativeBalance = policy.AllowsNegativeBalance;
        entity.CountsNonWorkingDays = policy.CountsNonWorkingDays;
        entity.EligibilityText = policy.EligibilityText;
        entity.AccrualRuleText = policy.AccrualRuleText;
        entity.CarryForwardRuleText = policy.CarryForwardRuleText;
        entity.NoticeRuleText = policy.NoticeRuleText;
        entity.DocumentsJson = policy.DocumentsJson;
        entity.IsActive = policy.IsActive;
        entity.ToneKey = policy.ToneKey;
        entity.Emoji = policy.Emoji;
        entity.UpdatedUtc = now;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    // ---- Balances ---------------------------------------------------------

    public async Task<IReadOnlyList<LeaveBalanceRecord>> GetBalancesAsync(
        string tenantKey, long employeeId, int year, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.LeaveBalances
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId && item.Year == year)
            .ToListAsync(cancellationToken);

        return rows.Select(MapBalance).ToList();
    }

    public async Task<LeaveBalanceRecord?> GetBalanceAsync(
        string tenantKey, long employeeId, long policyId, int year, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LeaveBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey && item.EmployeeId == employeeId &&
                        item.PolicyId == policyId && item.Year == year,
                cancellationToken);

        return entity is null ? null : MapBalance(entity);
    }

    public async Task<long> UpsertBalanceAsync(
        LeaveBalanceRecord balance, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LeaveBalances.FirstOrDefaultAsync(
            item => item.TenantKey == balance.TenantKey && item.EmployeeId == balance.EmployeeId &&
                    item.PolicyId == balance.PolicyId && item.Year == balance.Year,
            cancellationToken);

        if (entity is null)
        {
            entity = new LeaveBalanceEntity
            {
                TenantKey = balance.TenantKey,
                EmployeeId = balance.EmployeeId,
                PolicyId = balance.PolicyId,
                Year = balance.Year
            };
            dbContext.LeaveBalances.Add(entity);
        }

        entity.Entitled = balance.Entitled;
        entity.Accrued = balance.Accrued;
        entity.CarriedForward = balance.CarriedForward;
        entity.AdjustmentDays = balance.AdjustmentDays;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<IReadOnlyList<LeaveConsumptionRecord>> GetConsumptionAsync(
        string tenantKey, long employeeId, int year, CancellationToken cancellationToken = default)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        // Charged against the year a day falls in, not the year the request was raised, so a
        // range that crosses new year is counted where it is actually taken.
        var rows = await (
            from day in dbContext.LeaveRequestDays.AsNoTracking()
            join request in dbContext.LeaveRequests.AsNoTracking()
                on new { day.TenantKey, RequestId = day.RequestId } equals
                   new { request.TenantKey, RequestId = request.Id }
            where day.TenantKey == tenantKey && day.EmployeeId == employeeId &&
                  day.Date >= yearStart && day.Date <= yearEnd &&
                  (request.State == "Pending" || request.State == "Approved")
            select new { request.PolicyId, request.State, day.Portion })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(item => item.PolicyId)
            .Select(group => new LeaveConsumptionRecord(
                group.Key,
                group.Where(item => item.State == "Approved").Sum(item => item.Portion),
                group.Where(item => item.State == "Pending").Sum(item => item.Portion)))
            .ToList();
    }

    // ---- Requests ---------------------------------------------------------

    public async Task<IReadOnlyList<LeaveRequestRecord>> QueryRequestsAsync(
        string tenantKey, LeaveRequestFilter filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.LeaveRequests.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        if (filter.EmployeeId is { } employeeId)
        {
            query = query.Where(item => item.EmployeeId == employeeId);
        }

        if (filter.EmployeeIds is { Count: > 0 } ids)
        {
            query = query.Where(item => ids.Contains(item.EmployeeId));
        }

        if (!string.IsNullOrWhiteSpace(filter.State))
        {
            query = query.Where(item => item.State == filter.State);
        }

        // Any overlap with the window, so a request spanning the edge is not lost.
        if (filter.From is { } from)
        {
            query = query.Where(item => item.ToDate >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(item => item.FromDate <= to);
        }

        var rows = await query
            .OrderByDescending(item => item.FromDate)
            .ThenByDescending(item => item.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(MapRequest).ToList();
    }

    public async Task<LeaveRequestRecord?> GetRequestAsync(
        string tenantKey, long requestId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LeaveRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == requestId, cancellationToken);
        return entity is null ? null : MapRequest(entity);
    }

    public async Task<long> SaveRequestAsync(
        LeaveRequestRecord request,
        IReadOnlyList<LeaveRequestDayRecord> days,
        CancellationToken cancellationToken = default)
    {
        var entity = new LeaveRequestEntity
        {
            TenantKey = request.TenantKey,
            EmployeeId = request.EmployeeId,
            PolicyId = request.PolicyId,
            LeaveTypeCode = request.LeaveTypeCode,
            FromDate = request.FromDate,
            ToDate = request.ToDate,
            IsFirstDayPartial = request.IsFirstDayPartial,
            IsLastDayPartial = request.IsLastDayPartial,
            Days = request.Days,
            Reason = request.Reason,
            State = request.State,
            AttachmentsJson = request.AttachmentsJson,
            SubmittedUtc = request.SubmittedUtc,
            UpdatedUtc = request.UpdatedUtc
        };

        dbContext.LeaveRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        // The charged dates are written in the same unit of work as the request. A request
        // without its days would look free to the overlap check.
        dbContext.LeaveRequestDays.AddRange(days.Select(day => new LeaveRequestDayEntity
        {
            TenantKey = request.TenantKey,
            RequestId = entity.Id,
            EmployeeId = request.EmployeeId,
            Date = day.Date,
            Portion = day.Portion
        }));

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task UpdateRequestStateAsync(
        LeaveRequestRecord request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LeaveRequests.FirstOrDefaultAsync(
            item => item.TenantKey == request.TenantKey && item.Id == request.Id, cancellationToken);

        if (entity is null)
        {
            return;
        }

        entity.State = request.State;
        entity.DecidedByUserId = request.DecidedByUserId;
        entity.DecidedByName = request.DecidedByName;
        entity.DecidedUtc = request.DecidedUtc;
        entity.DecisionReasonCode = request.DecisionReasonCode;
        entity.DecisionComment = request.DecisionComment;
        entity.UpdatedUtc = DateTimeOffset.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveRequestDayRecord>> GetClaimedDaysAsync(
        string tenantKey, long employeeId, DateOnly from, DateOnly to, long? excludeRequestId = null,
        CancellationToken cancellationToken = default)
    {
        var rangeStart = from;
        var rangeEnd = to;

        var rows = await (
            from day in dbContext.LeaveRequestDays.AsNoTracking()
            join request in dbContext.LeaveRequests.AsNoTracking()
                on new { day.TenantKey, RequestId = day.RequestId } equals
                   new { request.TenantKey, RequestId = request.Id }
            where day.TenantKey == tenantKey && day.EmployeeId == employeeId &&
                  day.Date >= rangeStart && day.Date <= rangeEnd &&
                  LiveStates.Contains(request.State) &&
                  (excludeRequestId == null || day.RequestId != excludeRequestId)
            select day)
            .ToListAsync(cancellationToken);

        return rows.Select(MapDay).ToList();
    }

    public async Task<IReadOnlyList<LeaveRequestDayRecord>> GetDaysForRequestsAsync(
        string tenantKey, IReadOnlyList<long> requestIds, CancellationToken cancellationToken = default)
    {
        if (requestIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.LeaveRequestDays
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && requestIds.Contains(item.RequestId))
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);

        return rows.Select(MapDay).ToList();
    }

    // ---- Comments ---------------------------------------------------------

    public async Task<IReadOnlyList<LeaveRequestCommentRecord>> GetCommentsAsync(
        string tenantKey, long requestId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.LeaveRequestComments
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.RequestId == requestId)
            .OrderBy(item => item.CreatedUtc)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);

        return rows.Select(MapComment).ToList();
    }

    public async Task AddCommentAsync(
        LeaveRequestCommentRecord comment, CancellationToken cancellationToken = default)
    {
        dbContext.LeaveRequestComments.Add(new LeaveRequestCommentEntity
        {
            TenantKey = comment.TenantKey,
            RequestId = comment.RequestId,
            Kind = comment.Kind,
            AuthorUserId = comment.AuthorUserId,
            AuthorName = comment.AuthorName,
            Comment = comment.Comment,
            CreatedUtc = comment.CreatedUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Holidays ---------------------------------------------------------

    public async Task<IReadOnlyList<LeaveHolidayRecord>> GetHolidaysAsync(
        string tenantKey, DateOnly from, DateOnly to, string? locationCode = null,
        CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.LeaveHolidays
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.Date >= from && item.Date <= to &&
                           (item.LocationCode == null || item.LocationCode == locationCode))
            .OrderBy(item => item.Date)
            .ToListAsync(cancellationToken);

        return rows.Select(MapHoliday).ToList();
    }

    public async Task<IReadOnlyList<LeaveHolidayRecord>> GetAllHolidaysAsync(
        string tenantKey, int year, CancellationToken cancellationToken = default)
    {
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var rows = await dbContext.LeaveHolidays
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           item.Date >= yearStart && item.Date <= yearEnd)
            .OrderBy(item => item.Date)
            .ThenBy(item => item.LocationCode)
            .ToListAsync(cancellationToken);

        return rows.Select(MapHoliday).ToList();
    }

    public async Task<long> UpsertHolidayAsync(
        LeaveHolidayRecord holiday, CancellationToken cancellationToken = default)
    {
        // Matched by id when editing, and by natural key otherwise, so moving a holiday to a
        // new date renames the existing row instead of leaving the old date behind.
        var entity = holiday.Id > 0
            ? await dbContext.LeaveHolidays.FirstOrDefaultAsync(
                item => item.TenantKey == holiday.TenantKey && item.Id == holiday.Id, cancellationToken)
            : await dbContext.LeaveHolidays.FirstOrDefaultAsync(
                item => item.TenantKey == holiday.TenantKey && item.Date == holiday.Date &&
                        item.LocationCode == holiday.LocationCode,
                cancellationToken);

        if (entity is null)
        {
            entity = new LeaveHolidayEntity { TenantKey = holiday.TenantKey };
            dbContext.LeaveHolidays.Add(entity);
        }

        entity.Date = holiday.Date;
        entity.LocationCode = holiday.LocationCode;
        entity.Name = holiday.Name;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task DeleteHolidayAsync(
        string tenantKey, long holidayId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.LeaveHolidays.FirstOrDefaultAsync(
            item => item.TenantKey == tenantKey && item.Id == holidayId, cancellationToken);

        if (entity is null)
        {
            return;
        }

        dbContext.LeaveHolidays.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, int>> CountOpenRequestsByPolicyAsync(
        string tenantKey, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.LeaveRequests
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && LiveStates.Contains(item.State))
            .GroupBy(item => item.PolicyId)
            .Select(group => new { PolicyId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(item => item.PolicyId, item => item.Count);
    }

    // ---- Dashboard aggregates ---------------------------------------------

    public async Task<int> CountOnLeaveAsync(
        string tenantKey, DateOnly date, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return 0;
        }

        // Counted over distinct people, not rows: a half day plus a half day is still one
        // person on leave.
        return await (
            from day in dbContext.LeaveRequestDays.AsNoTracking()
            join request in dbContext.LeaveRequests.AsNoTracking()
                on new { day.TenantKey, RequestId = day.RequestId } equals
                   new { request.TenantKey, RequestId = request.Id }
            where day.TenantKey == tenantKey && day.Date == date &&
                  request.State == "Approved" && employeeIds.Contains(day.EmployeeId)
            select day.EmployeeId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<long>> GetOnLeaveAsync(
        string tenantKey, DateOnly date, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        return await (
            from day in dbContext.LeaveRequestDays.AsNoTracking()
            join request in dbContext.LeaveRequests.AsNoTracking()
                on new { day.TenantKey, RequestId = day.RequestId } equals
                   new { request.TenantKey, RequestId = request.Id }
            where day.TenantKey == tenantKey && day.Date == date &&
                  request.State == "Approved" && employeeIds.Contains(day.EmployeeId)
            select day.EmployeeId)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountRequestsByStateAsync(
        string tenantKey, string state, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return 0;
        }

        return await dbContext.LeaveRequests
            .AsNoTracking()
            .CountAsync(
                item => item.TenantKey == tenantKey && item.State == state &&
                        employeeIds.Contains(item.EmployeeId),
                cancellationToken);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> SumDaysByPolicyAsync(
        string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return new Dictionary<long, decimal>();
        }

        var rangeStart = from;
        var rangeEnd = to;

        var rows = await (
            from day in dbContext.LeaveRequestDays.AsNoTracking()
            join request in dbContext.LeaveRequests.AsNoTracking()
                on new { day.TenantKey, RequestId = day.RequestId } equals
                   new { request.TenantKey, RequestId = request.Id }
            where day.TenantKey == tenantKey && day.Date >= rangeStart && day.Date <= rangeEnd &&
                  request.State == "Approved" && employeeIds.Contains(day.EmployeeId)
            group day.Portion by request.PolicyId into grouped
            select new { PolicyId = grouped.Key, Days = grouped.Sum() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(item => item.PolicyId, item => item.Days);
    }

    public async Task<IReadOnlyList<(int Year, int Month, decimal Days)>> SumDaysByMonthAsync(
        string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var rangeStart = from;
        var rangeEnd = to;

        var rows = await (
            from day in dbContext.LeaveRequestDays.AsNoTracking()
            join request in dbContext.LeaveRequests.AsNoTracking()
                on new { day.TenantKey, RequestId = day.RequestId } equals
                   new { request.TenantKey, RequestId = request.Id }
            where day.TenantKey == tenantKey && day.Date >= rangeStart && day.Date <= rangeEnd &&
                  request.State == "Approved" && employeeIds.Contains(day.EmployeeId)
            group day.Portion by new { day.Date.Year, day.Date.Month } into grouped
            select new { grouped.Key.Year, grouped.Key.Month, Days = grouped.Sum() })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(item => item.Year).ThenBy(item => item.Month)
            .Select(item => (item.Year, item.Month, item.Days))
            .ToList();
    }

    public async Task<IReadOnlyList<(DateOnly Date, long PolicyId, decimal Days)>>
        SumDaysByDateAndPolicyAsync(
            string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
            CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return [];
        }

        var rangeStart = from;
        var rangeEnd = to;

        var rows = await (
            from day in dbContext.LeaveRequestDays.AsNoTracking()
            join request in dbContext.LeaveRequests.AsNoTracking()
                on new { day.TenantKey, RequestId = day.RequestId } equals
                   new { request.TenantKey, RequestId = request.Id }
            where day.TenantKey == tenantKey && day.Date >= rangeStart && day.Date <= rangeEnd &&
                  request.State == "Approved" && employeeIds.Contains(day.EmployeeId)
            group day.Portion by new { day.Date, request.PolicyId } into grouped
            select new { grouped.Key.Date, grouped.Key.PolicyId, Days = grouped.Sum() })
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(item => item.Date)
            .Select(item => (item.Date, item.PolicyId, item.Days))
            .ToList();
    }

    public async Task<IReadOnlyDictionary<string, int>> CountByStateAsync(
        string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return new Dictionary<string, int>();
        }

        var rows = await dbContext.LeaveRequests
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           item.ToDate >= from && item.FromDate <= to &&
                           employeeIds.Contains(item.EmployeeId))
            .GroupBy(item => item.State)
            .Select(group => new { State = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(item => item.State, item => item.Count);
    }

    public async Task<IReadOnlyDictionary<long, decimal>> SumDaysByEmployeeAsync(
        string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default)
    {
        if (employeeIds.Count == 0)
        {
            return new Dictionary<long, decimal>();
        }

        var rangeStart = from;
        var rangeEnd = to;

        var rows = await (
            from day in dbContext.LeaveRequestDays.AsNoTracking()
            join request in dbContext.LeaveRequests.AsNoTracking()
                on new { day.TenantKey, RequestId = day.RequestId } equals
                   new { request.TenantKey, RequestId = request.Id }
            where day.TenantKey == tenantKey && day.Date >= rangeStart && day.Date <= rangeEnd &&
                  request.State == "Approved" && employeeIds.Contains(day.EmployeeId)
            group day.Portion by day.EmployeeId into grouped
            select new { EmployeeId = grouped.Key, Days = grouped.Sum() })
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(item => item.EmployeeId, item => item.Days);
    }

    // ---- Credits and adjustments ------------------------------------------

    public async Task<IReadOnlyList<LeaveCreditRecord>> GetCreditsAsync(
        string tenantKey, long employeeId, bool unconsumedOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.LeaveCredits
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId);

        if (unconsumedOnly)
        {
            query = query.Where(item => item.ConsumedByAdjustmentId == null);
        }

        var rows = await query.OrderBy(item => item.ExpiresOn).ToListAsync(cancellationToken);
        return rows.Select(MapCredit).ToList();
    }

    public async Task<IReadOnlyList<LeaveCreditRecord>> GetCreditsByIdAsync(
        string tenantKey, IReadOnlyList<long> creditIds, CancellationToken cancellationToken = default)
    {
        if (creditIds.Count == 0)
        {
            return [];
        }

        var rows = await dbContext.LeaveCredits
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && creditIds.Contains(item.Id))
            .ToListAsync(cancellationToken);

        return rows.Select(MapCredit).ToList();
    }

    public async Task<long> AddAdjustmentAsync(
        LeaveAdjustmentRecord adjustment,
        IReadOnlyList<long> creditIds,
        CancellationToken cancellationToken = default)
    {
        var entity = new LeaveAdjustmentEntity
        {
            TenantKey = adjustment.TenantKey,
            EmployeeId = adjustment.EmployeeId,
            Kind = adjustment.Kind,
            CreditIdsJson = adjustment.CreditIdsJson,
            Days = adjustment.Days,
            Amount = adjustment.Amount,
            Currency = adjustment.Currency,
            State = adjustment.State,
            Comment = adjustment.Comment,
            CreatedUtc = adjustment.CreatedUtc
        };

        dbContext.LeaveAdjustments.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Marking the credits consumed in the same call is what stops the same block of leave
        // being encashed twice.
        var credits = await dbContext.LeaveCredits
            .Where(item => item.TenantKey == adjustment.TenantKey && creditIds.Contains(item.Id) &&
                           item.ConsumedByAdjustmentId == null)
            .ToListAsync(cancellationToken);

        foreach (var credit in credits)
        {
            credit.ConsumedByAdjustmentId = entity.Id;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    // ---- Mapping ----------------------------------------------------------

    private static LeavePolicyRecord MapPolicy(LeavePolicyEntity entity) => new(
        entity.Id, entity.TenantKey, entity.Code, entity.Name, entity.Unit, entity.Version,
        entity.AnnualEntitlement, entity.AccrualMode, entity.CarryForwardCap, entity.NoticeDays,
        entity.DocumentRequiredAboveDays, entity.AllowsHalfDay, entity.AllowsNegativeBalance,
        entity.CountsNonWorkingDays, entity.EligibilityText, entity.AccrualRuleText,
        entity.CarryForwardRuleText, entity.NoticeRuleText, entity.DocumentsJson, entity.IsActive,
        entity.ToneKey, entity.Emoji);

    private static LeaveBalanceRecord MapBalance(LeaveBalanceEntity entity) => new(
        entity.Id, entity.TenantKey, entity.EmployeeId, entity.PolicyId, entity.Year,
        entity.Entitled, entity.Accrued, entity.CarriedForward, entity.AdjustmentDays);

    private static LeaveRequestRecord MapRequest(LeaveRequestEntity entity) => new(
        entity.Id, entity.TenantKey, entity.EmployeeId, entity.PolicyId, entity.LeaveTypeCode,
        entity.FromDate, entity.ToDate, entity.IsFirstDayPartial, entity.IsLastDayPartial,
        entity.Days, entity.Reason, entity.State, entity.AttachmentsJson, entity.SubmittedUtc,
        entity.DecidedByUserId, entity.DecidedByName, entity.DecidedUtc, entity.DecisionReasonCode,
        entity.DecisionComment, entity.UpdatedUtc);

    private static LeaveRequestDayRecord MapDay(LeaveRequestDayEntity entity) => new(
        entity.Id, entity.TenantKey, entity.RequestId, entity.EmployeeId, entity.Date, entity.Portion);

    private static LeaveRequestCommentRecord MapComment(LeaveRequestCommentEntity entity) => new(
        entity.Id, entity.TenantKey, entity.RequestId, entity.Kind, entity.AuthorUserId,
        entity.AuthorName, entity.Comment, entity.CreatedUtc);

    private static LeaveHolidayRecord MapHoliday(LeaveHolidayEntity entity) => new(
        entity.Id, entity.TenantKey, entity.Date, entity.Name, entity.LocationCode);

    private static LeaveCreditRecord MapCredit(LeaveCreditEntity entity) => new(
        entity.Id, entity.TenantKey, entity.EmployeeId, entity.PolicyId, entity.SourceCode,
        entity.EarnedOn, entity.ExpiresOn, entity.Days, entity.IsEncashable,
        entity.EncashmentAmount, entity.EncashmentCurrency, entity.ConsumedByAdjustmentId);
}
