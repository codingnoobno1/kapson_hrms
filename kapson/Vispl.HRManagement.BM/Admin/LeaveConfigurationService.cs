using System.Text.Json;
using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Workforce;
using Vispl.HRManagement.VO.Workforce;

namespace Vispl.HRManagement.BM.Admin;

/// <summary>
/// Editing of the rules the leave engine runs on.
///
/// Validation here is deliberately stricter than the storage allows, because these values are
/// applied to every future request without anyone re-reading them.
/// </summary>
public sealed class LeaveConfigurationService(ILeaveRepository repository) : ILeaveConfigurationService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<LeavePolicyAdminDto>> GetPoliciesAsync(
        string tenantKey, CancellationToken cancellationToken = default)
    {
        var key = Normalize(tenantKey);

        // Inactive policies are included: the panel is where one gets switched back on.
        var policies = await repository.GetPoliciesAsync(key, activeOnly: false, cancellationToken);
        var openCounts = await repository.CountOpenRequestsByPolicyAsync(key, cancellationToken);

        return policies
            .Select(policy => ToDto(policy, openCounts.GetValueOrDefault(policy.Id)))
            .ToList();
    }

    public async Task<long> SavePolicyAsync(
        LeavePolicyAdminDto policy, CancellationToken cancellationToken = default)
    {
        var key = Normalize(policy.TenantKey);
        var code = policy.Code.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("A leave policy needs a code.");
        }

        if (!LeavePolicyOptions.Units.Contains(policy.Unit))
        {
            throw new InvalidOperationException($"'{policy.Unit}' is not a leave unit.");
        }

        if (!LeavePolicyOptions.AccrualModes.Any(mode => mode.Code == policy.AccrualMode))
        {
            throw new InvalidOperationException($"'{policy.AccrualMode}' is not an accrual mode.");
        }

        // A monthly accrual with nothing to accrue would credit nobody.
        if (policy.AccrualMode == "Monthly" && policy.AnnualEntitlement <= 0)
        {
            throw new InvalidOperationException(
                "A monthly accrual needs an annual entitlement to spread across the year.");
        }

        // Evidence demanded above a threshold no request can reach is a rule nobody can satisfy
        // or ever trip, which usually means the number was entered against the wrong field.
        if (policy.DocumentRequiredAboveDays is { } threshold &&
            policy.AnnualEntitlement > 0 && threshold > policy.AnnualEntitlement)
        {
            throw new InvalidOperationException(
                "Evidence cannot be required above more days than the policy grants in a year.");
        }

        var existing = await repository.GetPolicyByCodeAsync(key, code, cancellationToken);

        // Turning a policy off while requests are waiting would strand them: nobody could
        // approve what nobody can price.
        if (existing is { IsActive: true } && !policy.IsActive)
        {
            var openCounts = await repository.CountOpenRequestsByPolicyAsync(key, cancellationToken);

            if (openCounts.GetValueOrDefault(existing.Id) > 0)
            {
                throw new InvalidOperationException(
                    "This policy still has leave awaiting a decision. Clear those requests before " +
                    "deactivating it.");
            }
        }

        var record = new LeavePolicyRecord(
            existing?.Id ?? 0,
            key,
            code,
            policy.Name.Trim(),
            policy.Unit,
            policy.Version.Trim(),
            policy.AnnualEntitlement,
            policy.AccrualMode,
            policy.CarryForwardCap,
            policy.NoticeDays,
            policy.DocumentRequiredAboveDays,
            policy.AllowsHalfDay,
            policy.AllowsNegativeBalance,
            policy.CountsNonWorkingDays,
            policy.EligibilityText.Trim(),
            policy.AccrualRuleText.Trim(),
            policy.CarryForwardRuleText.Trim(),
            policy.NoticeRuleText.Trim(),
            existing?.DocumentsJson ?? JsonSerializer.Serialize(Array.Empty<string>(), Json),
            policy.IsActive,
            Contracts.Mobile.Workforce.LeaveTones.Normalize(policy.ToneKey),
            string.IsNullOrWhiteSpace(policy.Emoji) ? Contracts.Mobile.Workforce.LeaveTones.DefaultEmoji : policy.Emoji.Trim());

        return await repository.UpsertPolicyAsync(record, cancellationToken);
    }

    public async Task<IReadOnlyList<LeaveHolidayAdminDto>> GetHolidaysAsync(
        string tenantKey, int year, CancellationToken cancellationToken = default)
    {
        var holidays = await repository.GetAllHolidaysAsync(Normalize(tenantKey), year, cancellationToken);
        return holidays.Select(ToDto).ToList();
    }

    public async Task<long> SaveHolidayAsync(
        LeaveHolidayAdminDto holiday, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(holiday.Name))
        {
            throw new InvalidOperationException("A holiday needs a name.");
        }

        var location = string.IsNullOrWhiteSpace(holiday.LocationCode)
            ? null
            : holiday.LocationCode.Trim().ToUpperInvariant();

        return await repository.UpsertHolidayAsync(
            new LeaveHolidayRecord(holiday.Id, Normalize(holiday.TenantKey), holiday.Date,
                holiday.Name.Trim(), location),
            cancellationToken);
    }

    public Task DeleteHolidayAsync(
        string tenantKey, long holidayId, CancellationToken cancellationToken = default) =>
        repository.DeleteHolidayAsync(Normalize(tenantKey), holidayId, cancellationToken);

    private static LeavePolicyAdminDto ToDto(LeavePolicyRecord policy, int openRequests) => new()
    {
        Id = policy.Id,
        TenantKey = policy.TenantKey,
        Code = policy.Code,
        Name = policy.Name,
        Unit = policy.Unit,
        Version = policy.Version,
        AnnualEntitlement = policy.AnnualEntitlement,
        AccrualMode = policy.AccrualMode,
        CarryForwardCap = policy.CarryForwardCap,
        NoticeDays = policy.NoticeDays,
        DocumentRequiredAboveDays = policy.DocumentRequiredAboveDays,
        AllowsHalfDay = policy.AllowsHalfDay,
        AllowsNegativeBalance = policy.AllowsNegativeBalance,
        CountsNonWorkingDays = policy.CountsNonWorkingDays,
        EligibilityText = policy.EligibilityText,
        AccrualRuleText = policy.AccrualRuleText,
        CarryForwardRuleText = policy.CarryForwardRuleText,
        NoticeRuleText = policy.NoticeRuleText,
        IsActive = policy.IsActive,
        OpenRequestCount = openRequests,
        ToneKey = Contracts.Mobile.Workforce.LeaveTones.Normalize(policy.ToneKey),
        Emoji = policy.Emoji
    };

    private static LeaveHolidayAdminDto ToDto(LeaveHolidayRecord holiday) => new()
    {
        Id = holiday.Id,
        TenantKey = holiday.TenantKey,
        Date = holiday.Date,
        Name = holiday.Name,
        LocationCode = holiday.LocationCode
    };

    private static string Normalize(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return value.Trim().ToLowerInvariant();
    }
}
