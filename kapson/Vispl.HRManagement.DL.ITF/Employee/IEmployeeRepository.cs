using Vispl.HRManagement.VO.Employee;

namespace Vispl.HRManagement.DL.ITF.Employee;

public interface IEmployeeRepository
{
    Task<IReadOnlyList<EmployeeRecord>> QueryAsync(
        string tenantKey, EmployeeQueryFilter filter, CancellationToken cancellationToken = default);

    Task<EmployeeRecord?> GetAsync(string tenantKey, long employeeId, CancellationToken cancellationToken = default);

    Task<EmployeeRecord?> GetByNumberAsync(string tenantKey, string employeeNumber, CancellationToken cancellationToken = default);

    Task<EmployeeRecord?> GetByCandidateAsync(string tenantKey, long candidateId, CancellationToken cancellationToken = default);

    /// <summary>The employee this identity account belongs to, if any.</summary>
    Task<EmployeeRecord?> GetByAccountAsync(
        string tenantKey, string accountId, CancellationToken cancellationToken = default);

    /// <summary>Candidate matches for the duplicate check: same email, phone or number.</summary>
    Task<IReadOnlyList<EmployeeRecord>> FindPossibleDuplicatesAsync(
        string tenantKey, string? email, string? phone, string? employeeNumber, CancellationToken cancellationToken = default);

    /// <summary>Headcount for a scope, without materialising the rows.</summary>
    Task<int> CountAsync(
        string tenantKey, EmployeeQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>The employee ids in a scope — the population every dashboard widget is computed over.</summary>
    Task<IReadOnlyList<long>> GetIdsAsync(
        string tenantKey, EmployeeQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Birthdays and work anniversaries falling within a window around a date. Matched on month
    /// and day so the query does not care which year the person was born or joined.
    /// </summary>
    Task<IReadOnlyList<PersonMomentRecord>> GetMomentsAsync(
        string tenantKey,
        EmployeeQueryFilter filter,
        DateOnly asOf,
        int belatedDays,
        int upcomingDays,
        bool anniversaries,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The companies and units present in the data. The filter bar offers only these, so it
    /// cannot propose a company that would then return nothing.
    /// </summary>
    Task<(IReadOnlyList<ScopeOptionRecord> Companies, IReadOnlyList<ScopeOptionRecord> Units)>
        GetScopeOptionsAsync(
            string tenantKey,
            DateOnly? joinedOnOrBefore = null,
            CancellationToken cancellationToken = default);

    Task<long> UpsertAsync(EmployeeRecord employee, CancellationToken cancellationToken = default);

    // Contacts
    Task<IReadOnlyList<EmployeeContactRecord>> GetContactsAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default);
    Task<long> UpsertContactAsync(EmployeeContactRecord contact, CancellationToken cancellationToken = default);
    Task RemoveContactAsync(string tenantKey, long employeeId, long contactId, CancellationToken cancellationToken = default);

    // Dependants
    Task<IReadOnlyList<EmployeeDependantRecord>> GetDependantsAsync(
        string tenantKey, long employeeId, CancellationToken cancellationToken = default);

    // Timeline
    Task<IReadOnlyList<EmployeeTimelineRecord>> GetTimelineAsync(
        string tenantKey, long employeeId, string? eventType, DateOnly? from, DateOnly? to, CancellationToken cancellationToken = default);
    Task AddTimelineAsync(EmployeeTimelineRecord entry, CancellationToken cancellationToken = default);

    // Change requests
    Task<long> AddChangeRequestAsync(EmployeeChangeRequestRecord request, CancellationToken cancellationToken = default);
    Task<EmployeeChangeRequestRecord?> GetChangeRequestAsync(
        string tenantKey, long changeRequestId, CancellationToken cancellationToken = default);

    /// <summary>The HR decision queue. Null or blank status returns every request, newest first.</summary>
    Task<IReadOnlyList<EmployeeChangeRequestRecord>> QueryChangeRequestsAsync(
        string tenantKey, string? status, CancellationToken cancellationToken = default);

    Task UpdateChangeRequestStatusAsync(
        string tenantKey, long changeRequestId, string status, string? decisionReason, string? decidedBy,
        DateTimeOffset decidedUtc, CancellationToken cancellationToken = default);
}
