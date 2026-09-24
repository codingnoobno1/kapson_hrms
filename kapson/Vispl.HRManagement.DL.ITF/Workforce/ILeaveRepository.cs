using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.DL.ITF.Workforce;

public interface ILeaveRepository
{
    // Policies
    Task<IReadOnlyList<LeavePolicyRecord>> GetPoliciesAsync(
        string tenantKey, bool activeOnly = true, CancellationToken cancellationToken = default);

    Task<LeavePolicyRecord?> GetPolicyAsync(
        string tenantKey, long policyId, CancellationToken cancellationToken = default);

    Task<LeavePolicyRecord?> GetPolicyByCodeAsync(
        string tenantKey, string code, CancellationToken cancellationToken = default);

    Task<long> UpsertPolicyAsync(LeavePolicyRecord policy, CancellationToken cancellationToken = default);

    // Balances
    Task<IReadOnlyList<LeaveBalanceRecord>> GetBalancesAsync(
        string tenantKey, long employeeId, int year, CancellationToken cancellationToken = default);

    Task<LeaveBalanceRecord?> GetBalanceAsync(
        string tenantKey, long employeeId, long policyId, int year, CancellationToken cancellationToken = default);

    Task<long> UpsertBalanceAsync(LeaveBalanceRecord balance, CancellationToken cancellationToken = default);

    /// <summary>
    /// Days already approved and days still pending, per policy, for one employee-year.
    /// Derived from the request rows so it cannot disagree with them.
    /// </summary>
    Task<IReadOnlyList<LeaveConsumptionRecord>> GetConsumptionAsync(
        string tenantKey, long employeeId, int year, CancellationToken cancellationToken = default);

    // Requests
    Task<IReadOnlyList<LeaveRequestRecord>> QueryRequestsAsync(
        string tenantKey, LeaveRequestFilter filter, CancellationToken cancellationToken = default);

    Task<LeaveRequestRecord?> GetRequestAsync(
        string tenantKey, long requestId, CancellationToken cancellationToken = default);

    /// <summary>Saves the request together with the dates it charges, in one transaction.</summary>
    Task<long> SaveRequestAsync(
        LeaveRequestRecord request,
        IReadOnlyList<LeaveRequestDayRecord> days,
        CancellationToken cancellationToken = default);

    Task UpdateRequestStateAsync(
        LeaveRequestRecord request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dates already claimed by a live request for this employee, so a second request cannot
    /// book the same day twice.
    /// </summary>
    Task<IReadOnlyList<LeaveRequestDayRecord>> GetClaimedDaysAsync(
        string tenantKey, long employeeId, DateOnly from, DateOnly to, long? excludeRequestId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveRequestDayRecord>> GetDaysForRequestsAsync(
        string tenantKey, IReadOnlyList<long> requestIds, CancellationToken cancellationToken = default);

    // Comments
    Task<IReadOnlyList<LeaveRequestCommentRecord>> GetCommentsAsync(
        string tenantKey, long requestId, CancellationToken cancellationToken = default);

    Task AddCommentAsync(LeaveRequestCommentRecord comment, CancellationToken cancellationToken = default);

    // Holidays
    Task<IReadOnlyList<LeaveHolidayRecord>> GetHolidaysAsync(
        string tenantKey, DateOnly from, DateOnly to, string? locationCode = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Every holiday in a year, whatever its location. Unlike the calculation read, this does
    /// not fold location-specific rows into a single view: the editor has to see each row it
    /// can change.
    /// </summary>
    Task<IReadOnlyList<LeaveHolidayRecord>> GetAllHolidaysAsync(
        string tenantKey, int year, CancellationToken cancellationToken = default);

    Task<long> UpsertHolidayAsync(LeaveHolidayRecord holiday, CancellationToken cancellationToken = default);

    Task DeleteHolidayAsync(
        string tenantKey, long holidayId, CancellationToken cancellationToken = default);

    /// <summary>Live requests per policy, so a rule is not changed without seeing the cost.</summary>
    Task<IReadOnlyDictionary<long, int>> CountOpenRequestsByPolicyAsync(
        string tenantKey, CancellationToken cancellationToken = default);

    // Dashboard aggregates. Each is answered by the database rather than by counting rows in
    // memory: the population is the whole company, not one person's history.

    /// <summary>Employees with an approved leave day covering this date.</summary>
    Task<int> CountOnLeaveAsync(
        string tenantKey, DateOnly date, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Which of these employees had approved leave on this date. The count answers the tile;
    /// this answers "was this person's empty day an absence or a booked day off", which is the
    /// question the day-close job has to get right.
    /// </summary>
    Task<IReadOnlyList<long>> GetOnLeaveAsync(
        string tenantKey, DateOnly date, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default);

    Task<int> CountRequestsByStateAsync(
        string tenantKey, string state, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Approved leave days per policy across a range — the category donut.</summary>
    Task<IReadOnlyDictionary<long, decimal>> SumDaysByPolicyAsync(
        string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Approved leave days per calendar month — the trend line.</summary>
    Task<IReadOnlyList<(int Year, int Month, decimal Days)>> SumDaysByMonthAsync(
        string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Approved leave days per date per policy — the stacked daily chart. One query rather than
    /// one per day, because a month of a company's leave is otherwise thirty round trips.
    /// </summary>
    Task<IReadOnlyList<(DateOnly Date, long PolicyId, decimal Days)>> SumDaysByDateAndPolicyAsync(
        string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Request counts by state — the status pie.</summary>
    Task<IReadOnlyDictionary<string, int>> CountByStateAsync(
        string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default);

    /// <summary>Approved and pending days per employee across a range.</summary>
    Task<IReadOnlyDictionary<long, decimal>> SumDaysByEmployeeAsync(
        string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds,
        CancellationToken cancellationToken = default);

    // Credits and adjustments
    Task<IReadOnlyList<LeaveCreditRecord>> GetCreditsAsync(
        string tenantKey, long employeeId, bool unconsumedOnly = true,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LeaveCreditRecord>> GetCreditsByIdAsync(
        string tenantKey, IReadOnlyList<long> creditIds, CancellationToken cancellationToken = default);

    Task<long> AddAdjustmentAsync(
        LeaveAdjustmentRecord adjustment,
        IReadOnlyList<long> creditIds,
        CancellationToken cancellationToken = default);
}
