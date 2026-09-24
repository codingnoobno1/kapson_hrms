using Microsoft.EntityFrameworkCore;
using Vispl.HRManagement.DL.ITF.Employee;
using Vispl.HRManagement.DL.Shared;
using Vispl.HRManagement.VO.Employee;

namespace Vispl.HRManagement.DL.Employee;

public sealed class EmployeeRepository(HrManagementDbContext dbContext) : IEmployeeRepository
{
    /// <summary>
    /// The one place a scope becomes a query. Every dashboard read goes through this, so a
    /// company or unit filter cannot be honoured by one widget and forgotten by another.
    /// </summary>
    private IQueryable<EmployeeEntity> Scoped(string tenantKey, EmployeeQueryFilter filter)
    {
        var query = dbContext.Employees.AsNoTracking().Where(item => item.TenantKey == tenantKey);

        if (!string.IsNullOrWhiteSpace(filter.CompanyCode))
        {
            query = query.Where(item => item.CompanyCode == filter.CompanyCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.UnitCode))
        {
            query = query.Where(item => item.DivisionCode == filter.UnitCode);
        }

        if (filter.ActiveOnly)
        {
            query = query.Where(item => item.IsActive);
        }

        if (filter.JoinedOnOrBefore is { } startedBy)
        {
            query = query.Where(item => item.JoinedOn <= startedBy);
        }

        return query;
    }

    public async Task<int> CountAsync(
        string tenantKey, EmployeeQueryFilter filter, CancellationToken cancellationToken = default) =>
        await Scoped(tenantKey, filter).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<long>> GetIdsAsync(
        string tenantKey, EmployeeQueryFilter filter, CancellationToken cancellationToken = default) =>
        await Scoped(tenantKey, filter).Select(item => item.Id).ToListAsync(cancellationToken);

    public async Task<(IReadOnlyList<ScopeOptionRecord> Companies, IReadOnlyList<ScopeOptionRecord> Units)>
        GetScopeOptionsAsync(
            string tenantKey,
            DateOnly? joinedOnOrBefore = null,
            CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Employees
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.IsActive)
            .Where(item => joinedOnOrBefore == null || item.JoinedOn <= joinedOnOrBefore)
            .Select(item => new { item.CompanyCode, item.DivisionCode })
            .ToListAsync(cancellationToken);

        var companies = rows
            .Where(item => !string.IsNullOrWhiteSpace(item.CompanyCode))
            .GroupBy(item => item.CompanyCode!)
            .Select(group => new ScopeOptionRecord(group.Key, group.Key, group.Count()))
            .OrderBy(item => item.Code)
            .ToList();

        var units = rows
            .Where(item => !string.IsNullOrWhiteSpace(item.DivisionCode))
            .GroupBy(item => item.DivisionCode!)
            .Select(group => new ScopeOptionRecord(group.Key, group.Key, group.Count()))
            .OrderBy(item => item.Code)
            .ToList();

        return (companies, units);
    }

    public async Task<IReadOnlyList<PersonMomentRecord>> GetMomentsAsync(
        string tenantKey,
        EmployeeQueryFilter filter,
        DateOnly asOf,
        int belatedDays,
        int upcomingDays,
        bool anniversaries,
        CancellationToken cancellationToken = default)
    {
        var query = Scoped(tenantKey, filter);

        query = anniversaries
            ? query.Where(item => item.JoinedOn != null)
            : query.Where(item => item.DateOfBirth != null);

        var candidates = await query
            .Select(item => new
            {
                item.Id,
                item.LegalFirstName,
                item.LegalLastName,
                item.PreferredName,
                item.AvatarUrl,
                item.DepartmentCode,
                item.JobTitle,
                Anchor = anniversaries ? item.JoinedOn : item.DateOfBirth
            })
            .ToListAsync(cancellationToken);

        var moments = new List<PersonMomentRecord>();

        foreach (var person in candidates)
        {
            if (person.Anchor is not { } anchor)
            {
                continue;
            }

            // The occurrence in the as-of year, and the neighbouring years, so a window that
            // crosses new year still matches. 29 February falls back to the 28th.
            foreach (var year in new[] { asOf.Year - 1, asOf.Year, asOf.Year + 1 })
            {
                var day = anchor.Day;
                if (anchor.Month == 2 && day == 29 && !DateTime.IsLeapYear(year))
                {
                    day = 28;
                }

                var occurrence = new DateOnly(year, anchor.Month, day);
                var offset = occurrence.DayNumber - asOf.DayNumber;

                var window = offset switch
                {
                    0 => "Today",
                    < 0 when offset >= -belatedDays => "Belated",
                    > 0 when offset <= upcomingDays => "Upcoming",
                    _ => null
                };

                if (window is null)
                {
                    continue;
                }

                var years = year - anchor.Year;

                // Someone who joined or was born this year has no anniversary to mark yet.
                // Listing them as "0 years" is noise, not a milestone.
                if (years < 1)
                {
                    break;
                }

                var name = string.IsNullOrWhiteSpace(person.PreferredName)
                    ? $"{person.LegalFirstName} {person.LegalLastName}".Trim()
                    : person.PreferredName;

                moments.Add(new PersonMomentRecord(
                    person.Id, name, person.AvatarUrl, person.DepartmentCode, person.JobTitle,
                    anchor, years, window));
                break;
            }
        }

        return moments.OrderBy(item => item.Date.Month).ThenBy(item => item.Date.Day).ToList();
    }

    public async Task<IReadOnlyList<EmployeeRecord>> QueryAsync(
        string tenantKey, EmployeeQueryFilter filter, CancellationToken cancellationToken = default)
    {
        var query = Scoped(tenantKey, filter);

        if (!string.IsNullOrWhiteSpace(filter.DepartmentCode))
        {
            query = query.Where(item => item.DepartmentCode == filter.DepartmentCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.LocationCode))
        {
            query = query.Where(item => item.LocationCode == filter.LocationCode);
        }

        if (!string.IsNullOrWhiteSpace(filter.StatusCode))
        {
            query = query.Where(item => item.EmploymentStatusCode == filter.StatusCode);
        }

        if (filter.ManagerEmployeeId is { } managerId)
        {
            query = query.Where(item => item.ManagerEmployeeId == managerId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = $"%{filter.Search}%";
            query = query.Where(item =>
                EF.Functions.Like(item.LegalFirstName, term) ||
                EF.Functions.Like(item.LegalLastName, term) ||
                EF.Functions.Like(item.EmployeeNumber, term) ||
                (item.JobTitle != null && EF.Functions.Like(item.JobTitle, term)));
        }

        var rows = await query
            .OrderBy(item => item.LegalFirstName)
            .ThenBy(item => item.LegalLastName)
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<EmployeeRecord?> GetAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TenantKey == tenantKey && item.Id == employeeId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<EmployeeRecord?> GetByNumberAsync(
        string tenantKey, string employeeNumber, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey && item.EmployeeNumber == employeeNumber, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<EmployeeRecord?> GetByCandidateAsync(
        string tenantKey, long candidateId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey && item.CandidateId == candidateId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<EmployeeRecord?> GetByAccountAsync(
        string tenantKey, string accountId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey && item.AccountId == accountId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<EmployeeRecord>> FindPossibleDuplicatesAsync(
        string tenantKey, string? email, string? phone, string? employeeNumber, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email?.Trim().ToLowerInvariant();

        var rows = await dbContext.Employees
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey &&
                           ((normalizedEmail != null && item.Email != null && item.Email.ToLower() == normalizedEmail) ||
                            (phone != null && item.Phone == phone) ||
                            (employeeNumber != null && item.EmployeeNumber == employeeNumber)))
            .ToListAsync(cancellationToken);

        return rows.Select(Map).ToList();
    }

    public async Task<long> UpsertAsync(EmployeeRecord employee, CancellationToken cancellationToken = default)
    {
        var entity = employee.Id > 0
            ? await dbContext.Employees.FirstOrDefaultAsync(
                item => item.TenantKey == employee.TenantKey && item.Id == employee.Id, cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new EmployeeEntity
            {
                TenantKey = employee.TenantKey,
                EmployeeNumber = employee.EmployeeNumber,
                CreatedUtc = employee.CreatedUtc
            };
            dbContext.Employees.Add(entity);
        }

        entity.LegalFirstName = employee.LegalFirstName;
        entity.LegalMiddleName = employee.LegalMiddleName;
        entity.LegalLastName = employee.LegalLastName;
        entity.PreferredName = employee.PreferredName;
        entity.DateOfBirth = employee.DateOfBirth;
        entity.GenderCode = employee.GenderCode;
        entity.MaritalStatusCode = employee.MaritalStatusCode;
        entity.NationalityCode = employee.NationalityCode;
        entity.Email = employee.Email;
        entity.Phone = employee.Phone;
        entity.AvatarUrl = employee.AvatarUrl;
        entity.JoinedOn = employee.JoinedOn;
        entity.EmploymentTypeCode = employee.EmploymentTypeCode;
        entity.EmploymentStatusCode = employee.EmploymentStatusCode;
        entity.GradeCode = employee.GradeCode;
        entity.JobTitle = employee.JobTitle;
        entity.ProbationEndsOn = employee.ProbationEndsOn;
        entity.LeavingOn = employee.LeavingOn;
        entity.CompanyCode = employee.CompanyCode;
        entity.DivisionCode = employee.DivisionCode;
        entity.DepartmentCode = employee.DepartmentCode;
        entity.LocationCode = employee.LocationCode;
        entity.ManagerEmployeeId = employee.ManagerEmployeeId;
        entity.CostCentre = employee.CostCentre;
        entity.AccountId = employee.AccountId;
        entity.CandidateId = employee.CandidateId;
        entity.IsActive = employee.IsActive;
        entity.UpdatedUtc = employee.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    // ---- Contacts ---------------------------------------------------------

    public async Task<IReadOnlyList<EmployeeContactRecord>> GetContactsAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.EmployeeContacts
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .OrderByDescending(item => item.IsPrimary)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);

        return rows.Select(MapContact).ToList();
    }

    public async Task<long> UpsertContactAsync(
        EmployeeContactRecord contact, CancellationToken cancellationToken = default)
    {
        var entity = contact.Id > 0
            ? await dbContext.EmployeeContacts.FirstOrDefaultAsync(
                item => item.TenantKey == contact.TenantKey &&
                        item.EmployeeId == contact.EmployeeId &&
                        item.Id == contact.Id,
                cancellationToken)
            : null;

        if (entity is null)
        {
            entity = new EmployeeContactEntity
            {
                TenantKey = contact.TenantKey,
                EmployeeId = contact.EmployeeId
            };
            dbContext.EmployeeContacts.Add(entity);
        }

        entity.Kind = contact.Kind;
        entity.Name = contact.Name;
        entity.Relationship = contact.Relationship;
        entity.Phone = contact.Phone;
        entity.Email = contact.Email;
        entity.DateOfBirth = contact.DateOfBirth;
        entity.IsPrimary = contact.IsPrimary;
        entity.IsVerified = contact.IsVerified;
        entity.UpdatedUtc = contact.UpdatedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);

        // Only one primary contact per employee, enforced on write rather than by convention.
        if (entity.IsPrimary)
        {
            var others = await dbContext.EmployeeContacts
                .Where(item => item.TenantKey == contact.TenantKey &&
                               item.EmployeeId == contact.EmployeeId &&
                               item.Id != entity.Id &&
                               item.IsPrimary)
                .ToListAsync(cancellationToken);

            foreach (var other in others)
            {
                other.IsPrimary = false;
            }

            if (others.Count > 0)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return entity.Id;
    }

    public async Task RemoveContactAsync(
        string tenantKey, long employeeId, long contactId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.EmployeeContacts.FirstOrDefaultAsync(
            item => item.TenantKey == tenantKey && item.EmployeeId == employeeId && item.Id == contactId,
            cancellationToken);

        if (entity is null)
        {
            return;
        }

        dbContext.EmployeeContacts.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Dependants -------------------------------------------------------

    public async Task<IReadOnlyList<EmployeeDependantRecord>> GetDependantsAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.EmployeeDependants
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId)
            .OrderBy(item => item.DisplayName)
            .ToListAsync(cancellationToken);

        return rows
            .Select(item => new EmployeeDependantRecord(
                item.Id, item.TenantKey, item.EmployeeId, item.DisplayName, item.RelationCode,
                item.DateOfBirth, item.IsBenefitEligible, item.UpdatedUtc))
            .ToList();
    }

    // ---- Timeline ---------------------------------------------------------

    public async Task<IReadOnlyList<EmployeeTimelineRecord>> GetTimelineAsync(
        string tenantKey, long employeeId, string? eventType, DateOnly? from, DateOnly? to,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.EmployeeTimeline
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey && item.EmployeeId == employeeId);

        if (!string.IsNullOrWhiteSpace(eventType))
        {
            query = query.Where(item => item.EventType == eventType);
        }

        if (from is { } fromDate)
        {
            query = query.Where(item => item.EffectiveOn >= fromDate);
        }

        if (to is { } toDate)
        {
            query = query.Where(item => item.EffectiveOn <= toDate);
        }

        var rows = await query
            .OrderByDescending(item => item.OccurredUtc)
            .ToListAsync(cancellationToken);

        return rows
            .Select(item => new EmployeeTimelineRecord(
                item.Id, item.TenantKey, item.EmployeeId, item.EventType, item.Title, item.Summary,
                item.EffectiveOn, item.ActorId, item.ActorName, item.OccurredUtc))
            .ToList();
    }

    public async Task AddTimelineAsync(
        EmployeeTimelineRecord entry, CancellationToken cancellationToken = default)
    {
        dbContext.EmployeeTimeline.Add(new EmployeeTimelineEntity
        {
            TenantKey = entry.TenantKey,
            EmployeeId = entry.EmployeeId,
            EventType = entry.EventType,
            Title = entry.Title,
            Summary = entry.Summary,
            EffectiveOn = entry.EffectiveOn,
            ActorId = entry.ActorId,
            ActorName = entry.ActorName,
            OccurredUtc = entry.OccurredUtc
        });

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Change requests --------------------------------------------------

    public async Task<long> AddChangeRequestAsync(
        EmployeeChangeRequestRecord request, CancellationToken cancellationToken = default)
    {
        var entity = new EmployeeChangeRequestEntity
        {
            TenantKey = request.TenantKey,
            EmployeeId = request.EmployeeId,
            Section = request.Section,
            ChangesJson = request.ChangesJson,
            Reason = request.Reason,
            Status = request.Status,
            RequestedBy = request.RequestedBy,
            SubmittedUtc = request.SubmittedUtc
        };

        dbContext.EmployeeChangeRequests.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    public async Task<EmployeeChangeRequestRecord?> GetChangeRequestAsync(
        string tenantKey, long changeRequestId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.EmployeeChangeRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey && item.Id == changeRequestId, cancellationToken);

        return entity is null
            ? null
            : new EmployeeChangeRequestRecord(
                entity.Id, entity.TenantKey, entity.EmployeeId, entity.Section, entity.ChangesJson,
                entity.Reason, entity.Status, entity.DecisionReason, entity.RequestedBy, entity.DecidedBy,
                entity.SubmittedUtc, entity.DecidedUtc);
    }

    public async Task<IReadOnlyList<EmployeeChangeRequestRecord>> QueryChangeRequestsAsync(
        string tenantKey, string? status, CancellationToken cancellationToken = default)
    {
        var query = dbContext.EmployeeChangeRequests
            .AsNoTracking()
            .Where(item => item.TenantKey == tenantKey);

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(item => item.Status == status);
        }

        var entities = await query
            .OrderByDescending(item => item.SubmittedUtc)
            .ToListAsync(cancellationToken);

        return entities
            .Select(entity => new EmployeeChangeRequestRecord(
                entity.Id, entity.TenantKey, entity.EmployeeId, entity.Section, entity.ChangesJson,
                entity.Reason, entity.Status, entity.DecisionReason, entity.RequestedBy, entity.DecidedBy,
                entity.SubmittedUtc, entity.DecidedUtc))
            .ToList();
    }

    public async Task UpdateChangeRequestStatusAsync(
        string tenantKey, long changeRequestId, string status, string? decisionReason, string? decidedBy,
        DateTimeOffset decidedUtc, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.EmployeeChangeRequests
            .FirstOrDefaultAsync(
                item => item.TenantKey == tenantKey && item.Id == changeRequestId, cancellationToken)
            ?? throw new InvalidOperationException($"Change request {changeRequestId} no longer exists.");

        entity.Status = status;
        entity.DecisionReason = decisionReason;
        entity.DecidedBy = decidedBy;
        entity.DecidedUtc = decidedUtc;

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    // ---- Mapping ----------------------------------------------------------

    private static EmployeeRecord Map(EmployeeEntity item) => new(
        item.Id, item.TenantKey, item.EmployeeNumber,
        item.LegalFirstName, item.LegalMiddleName, item.LegalLastName, item.PreferredName,
        item.DateOfBirth, item.GenderCode, item.MaritalStatusCode, item.NationalityCode,
        item.Email, item.Phone, item.AvatarUrl,
        item.JoinedOn, item.EmploymentTypeCode, item.EmploymentStatusCode, item.GradeCode,
        item.JobTitle, item.ProbationEndsOn,
        item.CompanyCode, item.DivisionCode, item.DepartmentCode, item.LocationCode,
        item.ManagerEmployeeId, item.CostCentre, item.AccountId, item.CandidateId,
        item.IsActive, item.CreatedUtc, item.UpdatedUtc, item.LeavingOn);

    private static EmployeeContactRecord MapContact(EmployeeContactEntity item) => new(
        item.Id, item.TenantKey, item.EmployeeId, item.Kind, item.Name, item.Relationship,
        item.Phone, item.Email, item.DateOfBirth, item.IsPrimary, item.IsVerified, item.UpdatedUtc);
}
