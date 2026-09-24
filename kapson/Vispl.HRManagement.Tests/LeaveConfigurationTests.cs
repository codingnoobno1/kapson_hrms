using Vispl.HRManagement.BM.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Workforce;
using Xunit;

namespace Vispl.HRManagement.Tests;

/// <summary>
/// The admin panel writes the rules the leave engine then enforces without anyone re-reading
/// them, so the guards on the way in are what stop a policy that cannot work.
/// </summary>
public sealed class LeaveConfigurationTests
{
    private static LeavePolicyAdminDto ValidPolicy() => new()
    {
        TenantKey = "kapson",
        Code = "AL",
        Name = "Annual Leave",
        Unit = "Days",
        Version = "2026.1",
        AnnualEntitlement = 18,
        AccrualMode = "Monthly",
        CarryForwardCap = 10,
        NoticeDays = 3,
        AllowsHalfDay = true,
        IsActive = true
    };

    /// <summary>
    /// A carry-forward cap limits the bank an employee may accumulate across years, so it is
    /// expected to exceed a single year's entitlement. The seeded Annual Leave policy grants 18
    /// days and banks up to 30.
    /// </summary>
    [Fact]
    public async Task Carry_forward_may_exceed_one_years_entitlement()
    {
        var repository = new FakeLeaveRepository();
        var service = new LeaveConfigurationService(repository);

        var policy = ValidPolicy();
        policy.CarryForwardCap = 30;

        await service.SavePolicyAsync(policy);

        Assert.Equal(30, repository.LastSavedPolicy!.CarryForwardCap);
    }

    [Fact]
    public async Task Evidence_demanded_above_a_years_entitlement_is_refused()
    {
        var service = new LeaveConfigurationService(new FakeLeaveRepository());
        var policy = ValidPolicy();
        policy.DocumentRequiredAboveDays = 60;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SavePolicyAsync(policy));

        Assert.Contains("Evidence", error.Message);
    }

    [Fact]
    public async Task Monthly_accrual_with_no_entitlement_is_refused()
    {
        var service = new LeaveConfigurationService(new FakeLeaveRepository());
        var policy = ValidPolicy();
        policy.AnnualEntitlement = 0;
        policy.CarryForwardCap = 0;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SavePolicyAsync(policy));
    }

    [Theory]
    [InlineData("Weeks", "Days")]
    [InlineData("Days", "Quarterly")]
    public async Task An_unknown_unit_or_accrual_mode_is_refused(string unit, string accrual)
    {
        var service = new LeaveConfigurationService(new FakeLeaveRepository());
        var policy = ValidPolicy();
        policy.Unit = unit;
        policy.AccrualMode = accrual;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SavePolicyAsync(policy));
    }

    [Fact]
    public async Task A_policy_with_pending_requests_cannot_be_deactivated()
    {
        var repository = new FakeLeaveRepository();
        repository.Policies.Add(new LeavePolicyRecord(
            7, "kapson", "AL", "Annual Leave", "Days", "2026.1", 18, "Monthly", 10, 3, null,
            true, false, false, "", "", "", "", "[]", IsActive: true));
        repository.OpenRequests[7] = 2;

        var service = new LeaveConfigurationService(repository);
        var policy = ValidPolicy();
        policy.IsActive = false;

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SavePolicyAsync(policy));

        Assert.Contains("awaiting a decision", error.Message);
    }

    [Fact]
    public async Task A_policy_with_no_pending_requests_may_be_deactivated()
    {
        var repository = new FakeLeaveRepository();
        repository.Policies.Add(new LeavePolicyRecord(
            7, "kapson", "AL", "Annual Leave", "Days", "2026.1", 18, "Monthly", 10, 3, null,
            true, false, false, "", "", "", "", "[]", IsActive: true));

        var service = new LeaveConfigurationService(repository);
        var policy = ValidPolicy();
        policy.IsActive = false;

        await service.SavePolicyAsync(policy);

        Assert.False(repository.LastSavedPolicy!.IsActive);
    }

    [Fact]
    public async Task Codes_and_tenants_are_normalised_so_a_stray_case_cannot_create_a_duplicate()
    {
        var repository = new FakeLeaveRepository();
        var service = new LeaveConfigurationService(repository);

        var policy = ValidPolicy();
        policy.TenantKey = "Kapson";
        policy.Code = "al";

        await service.SavePolicyAsync(policy);

        Assert.Equal("kapson", repository.LastSavedPolicy!.TenantKey);
        Assert.Equal("AL", repository.LastSavedPolicy.Code);
    }

    [Fact]
    public async Task Editing_a_policy_keeps_its_published_documents()
    {
        var repository = new FakeLeaveRepository();
        repository.Policies.Add(new LeavePolicyRecord(
            7, "kapson", "AL", "Annual Leave", "Days", "2026.1", 18, "Monthly", 10, 3, null,
            true, false, false, "", "", "", "", """["leave-policy.pdf"]""", IsActive: true));

        var service = new LeaveConfigurationService(repository);
        await service.SavePolicyAsync(ValidPolicy());

        // The editor has no field for documents, so a save must not silently drop them.
        Assert.Contains("leave-policy.pdf", repository.LastSavedPolicy!.DocumentsJson);
    }

    [Fact]
    public async Task A_blank_location_means_the_holiday_applies_everywhere()
    {
        var repository = new FakeLeaveRepository();
        var service = new LeaveConfigurationService(repository);

        await service.SaveHolidayAsync(new LeaveHolidayAdminDto
        {
            TenantKey = "kapson",
            Date = new DateOnly(2026, 1, 26),
            Name = "Republic Day",
            LocationCode = "   "
        });

        Assert.Null(repository.LastSavedHoliday!.LocationCode);
    }

    [Fact]
    public async Task A_holiday_without_a_name_is_refused()
    {
        var service = new LeaveConfigurationService(new FakeLeaveRepository());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveHolidayAsync(
            new LeaveHolidayAdminDto
            {
                TenantKey = "kapson",
                Date = new DateOnly(2026, 1, 26),
                Name = "  "
            }));
    }

    [Fact]
    public async Task Inactive_policies_are_listed_so_one_can_be_switched_back_on()
    {
        var repository = new FakeLeaveRepository();
        repository.Policies.Add(new LeavePolicyRecord(
            7, "kapson", "SAB", "Sabbatical", "Days", "1", 0, "None", 0, 30, null,
            false, false, true, "", "", "", "", "[]", IsActive: false));

        var service = new LeaveConfigurationService(repository);
        var listed = await service.GetPoliciesAsync("kapson");

        Assert.Single(listed);
        Assert.False(listed[0].IsActive);
    }

    /// <summary>In-memory stand-in: these tests are about the rules, not about storage.</summary>
    private sealed class FakeLeaveRepository : ILeaveRepository
    {
        public List<LeavePolicyRecord> Policies { get; } = [];
        public List<LeaveHolidayRecord> Holidays { get; } = [];
        public Dictionary<long, int> OpenRequests { get; } = [];
        public LeavePolicyRecord? LastSavedPolicy { get; private set; }
        public LeaveHolidayRecord? LastSavedHoliday { get; private set; }
        public long? DeletedHolidayId { get; private set; }

        public Task<IReadOnlyList<LeavePolicyRecord>> GetPoliciesAsync(
            string tenantKey, bool activeOnly = true, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LeavePolicyRecord>>(
                Policies.Where(item => !activeOnly || item.IsActive).ToList());

        public Task<LeavePolicyRecord?> GetPolicyAsync(
            string tenantKey, long policyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(Policies.FirstOrDefault(item => item.Id == policyId));

        public Task<LeavePolicyRecord?> GetPolicyByCodeAsync(
            string tenantKey, string code, CancellationToken cancellationToken = default) =>
            Task.FromResult(Policies.FirstOrDefault(item => item.Code == code));

        public Task<long> UpsertPolicyAsync(
            LeavePolicyRecord policy, CancellationToken cancellationToken = default)
        {
            LastSavedPolicy = policy;
            return Task.FromResult(policy.Id == 0 ? 1L : policy.Id);
        }

        public Task<IReadOnlyDictionary<long, int>> CountOpenRequestsByPolicyAsync(
            string tenantKey, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyDictionary<long, int>>(OpenRequests);

        public Task<IReadOnlyList<LeaveHolidayRecord>> GetAllHolidaysAsync(
            string tenantKey, int year, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<LeaveHolidayRecord>>(
                Holidays.Where(item => item.Date.Year == year).ToList());

        public Task<long> UpsertHolidayAsync(
            LeaveHolidayRecord holiday, CancellationToken cancellationToken = default)
        {
            LastSavedHoliday = holiday;
            return Task.FromResult(holiday.Id == 0 ? 1L : holiday.Id);
        }

        public Task DeleteHolidayAsync(
            string tenantKey, long holidayId, CancellationToken cancellationToken = default)
        {
            DeletedHolidayId = holidayId;
            return Task.CompletedTask;
        }

        // Dashboard aggregates: not exercised by the admin surface.
        public Task<IReadOnlyList<(DateOnly Date, long PolicyId, decimal Days)>> SumDaysByDateAndPolicyAsync(string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountOnLeaveAsync(string tenantKey, DateOnly date, IReadOnlyList<long> employeeIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<long>> GetOnLeaveAsync(string tenantKey, DateOnly date, IReadOnlyList<long> employeeIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> CountRequestsByStateAsync(string tenantKey, string state, IReadOnlyList<long> employeeIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<long, decimal>> SumDaysByPolicyAsync(string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<(int Year, int Month, decimal Days)>> SumDaysByMonthAsync(string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<string, int>> CountByStateAsync(string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyDictionary<long, decimal>> SumDaysByEmployeeAsync(string tenantKey, DateOnly from, DateOnly to, IReadOnlyList<long> employeeIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        // Not exercised by the admin surface.
        public Task<IReadOnlyList<LeaveBalanceRecord>> GetBalancesAsync(string tenantKey, long employeeId, int year, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LeaveBalanceRecord?> GetBalanceAsync(string tenantKey, long employeeId, long policyId, int year, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> UpsertBalanceAsync(LeaveBalanceRecord balance, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveConsumptionRecord>> GetConsumptionAsync(string tenantKey, long employeeId, int year, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequestRecord>> QueryRequestsAsync(string tenantKey, LeaveRequestFilter filter, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<LeaveRequestRecord?> GetRequestAsync(string tenantKey, long requestId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> SaveRequestAsync(LeaveRequestRecord request, IReadOnlyList<LeaveRequestDayRecord> days, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task UpdateRequestStateAsync(LeaveRequestRecord request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequestDayRecord>> GetClaimedDaysAsync(string tenantKey, long employeeId, DateOnly from, DateOnly to, long? excludeRequestId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequestDayRecord>> GetDaysForRequestsAsync(string tenantKey, IReadOnlyList<long> requestIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveRequestCommentRecord>> GetCommentsAsync(string tenantKey, long requestId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task AddCommentAsync(LeaveRequestCommentRecord comment, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveHolidayRecord>> GetHolidaysAsync(string tenantKey, DateOnly from, DateOnly to, string? locationCode = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveCreditRecord>> GetCreditsAsync(string tenantKey, long employeeId, bool unconsumedOnly = true, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<LeaveCreditRecord>> GetCreditsByIdAsync(string tenantKey, IReadOnlyList<long> creditIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<long> AddAdjustmentAsync(LeaveAdjustmentRecord adjustment, IReadOnlyList<long> creditIds, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
