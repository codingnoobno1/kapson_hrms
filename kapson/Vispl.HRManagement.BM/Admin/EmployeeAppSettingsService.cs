using Vispl.HRManagement.BM.ITF.Admin;
using Vispl.HRManagement.BM.ITF.Mobile;
using Vispl.HRManagement.Contracts.Admin;
using Vispl.HRManagement.DL.ITF.Admin;
using Vispl.HRManagement.VO.Admin;

namespace Vispl.HRManagement.BM.Admin;

public sealed class EmployeeAppSettingsService(IEmployeeAppSettingsRepository repository) : IEmployeeAppSettingsService
{
    private const string ManagePermission = "MASTERS.MANAGE";

    public async Task<EmployeeAppSettingsDto> GetAsync(string tenantKey, CancellationToken cancellationToken = default)
    {
        var record = await repository.GetAsync(Normalize(tenantKey), cancellationToken);

        if (record is null)
        {
            return new EmployeeAppSettingsDto { IsDefault = true };
        }

        return new EmployeeAppSettingsDto
        {
            AllowAppPunch = record.AllowAppPunch,
            RequireLocationForPunch = record.RequireLocationForPunch,
            AllowGatePassRequests = record.AllowGatePassRequests,
            AllowAttendanceCorrections = record.AllowAttendanceCorrections,
            ShowSalaryDetails = record.ShowSalaryDetails,
            ShowBankDetails = record.ShowBankDetails,
            ShowOnboarding = record.ShowOnboarding,
            AllowProfileChangeRequests = record.AllowProfileChangeRequests,
            AttendanceTargetPercent = record.AttendanceTargetPercent,
            UpdatedUtc = record.UpdatedUtc,
            UpdatedBy = record.UpdatedBy
        };
    }

    public async Task SaveAsync(MobileCallerContext caller, EmployeeAppSettingsDto settings, CancellationToken cancellationToken = default)
    {
        if (!caller.Has(ManagePermission))
        {
            throw new MobileAccessDeniedException(ManagePermission);
        }

        await repository.UpsertAsync(new EmployeeAppSettingsRecord
        {
            TenantKey = Normalize(caller.TenantKey),
            AllowAppPunch = settings.AllowAppPunch,

            // Location without punching allowed means nothing; stored as asked so re-enabling
            // punch brings the previous choice back.
            RequireLocationForPunch = settings.RequireLocationForPunch,
            AllowGatePassRequests = settings.AllowGatePassRequests,
            AllowAttendanceCorrections = settings.AllowAttendanceCorrections,
            ShowSalaryDetails = settings.ShowSalaryDetails,
            ShowBankDetails = settings.ShowBankDetails,
            ShowOnboarding = settings.ShowOnboarding,
            AllowProfileChangeRequests = settings.AllowProfileChangeRequests,
            AttendanceTargetPercent = Math.Clamp(settings.AttendanceTargetPercent, 50, 100),
            UpdatedUtc = DateTimeOffset.UtcNow,
            UpdatedBy = caller.UserId
        }, cancellationToken);
    }

    // Same tenant key as every other tenant-scoped table (bank accounts, payroll): as the caller carries it.
    private static string Normalize(string tenantKey) => tenantKey.Trim();
}
